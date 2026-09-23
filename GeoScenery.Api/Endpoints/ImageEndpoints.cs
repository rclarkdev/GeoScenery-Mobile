using System.Buffers;
using GeoScenery.Api.Storage;
using GeoScenery.Api.ViewModels;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace GeoScenery.Api.Endpoints;

/// <summary>
/// Authenticated multipart image uploads. Files are validated by actual content
/// decoding (not filename/extension), resized/downscaled, given server-generated
/// names, and stored via <see cref="IFileStorageService"/>. The stored reference
/// (a /uploads/... URL) is the only value the rest of the API accepts for scene
/// and profile images.
/// </summary>
public static class ImageEndpoints
{
    public static IEndpointRouteBuilder MapImageEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/images").RequireAuthorization();

        group.MapPost("", async Task<Results<Ok<UploadedImageResponse>, BadRequest<string>>>
            (HttpRequest request, IFileStorageService storage, IOptions<ImageUploadOptions> options, CancellationToken cancellationToken) =>
        {
            var form = await request.ReadFormAsync(cancellationToken);
            var file = request.Form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
            {
                return TypedResults.BadRequest("A file is required.");
            }

            if (file.Length > options.Value.MaxUploadBytes)
            {
                return TypedResults.BadRequest($"The image exceeds the maximum size of {options.Value.MaxUploadBytes} bytes.");
            }

            var kind = form["kind"].ToString().Trim().ToLowerInvariant();
            if (kind is not ("scene" or "profile"))
            {
                return TypedResults.BadRequest("kind must be 'scene' or 'profile'.");
            }

            var content = new byte[file.Length];
            await using (var source = file.OpenReadStream())
            {
                await ReadExactAsync(source, content, cancellationToken);
            }

            var processed = ProcessImage(content, kind, options.Value);
            if (processed is null)
            {
                return TypedResults.BadRequest("The file is not a supported image (JPEG, PNG, or WebP).");
            }

            var url = await storage.SaveAsync(processed.Value.Bytes, processed.Value.Extension, cancellationToken);
            return TypedResults.Ok(new UploadedImageResponse(url));
        })
        .WithName("UploadImage");

        return endpoints;
    }

    /// <summary>
    /// Decodes the image to validate its real content, downscales it when larger
    /// than the kind's maximum dimension, re-encodes it, and reports the extension
    /// for the stored file. Returns null when the content is not a supported image
    /// format or is corrupt.
    /// </summary>
    private static (byte[] Bytes, string Extension)? ProcessImage(byte[] content, string kind, ImageUploadOptions options)
    {
        try
        {
            // Image.Load validates the real image content; the file extension and
            // client-declared content type are not trusted for this decision.
            using var input = new MemoryStream(content, writable: false);
            var format = Image.DetectFormat(input);
            input.Position = 0;
            using var image = Image.Load(input);
            var extension = format switch
            {
                JpegFormat => "jpg",
                PngFormat => "png",
                WebpFormat => "webp",
                _ => null
            };
            if (extension is null)
            {
                return null;
            }

            var maxDimension = kind == "profile"
                ? options.ProfileMaxDimensionPixels
                : options.MaxDimensionPixels;
            if (image.Width > maxDimension || image.Height > maxDimension)
            {
                image.Mutate(context => context.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxDimension, maxDimension)
                }));
            }

            using var output = new MemoryStream();
            switch (format)
            {
                case JpegFormat:
                    image.Save(output, new JpegEncoder { Quality = 85 });
                    break;
                case PngFormat:
                    image.Save(output, new PngEncoder());
                    break;
                default:
                    image.Save(output, new WebpEncoder());
                    break;
            }

            return (output.ToArray(), extension);
        }
        catch (UnknownImageFormatException)
        {
            return null;
        }
        catch (InvalidImageContentException)
        {
            return null;
        }
    }

    private static async Task ReadExactAsync(Stream source, byte[] destination, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = await source.ReadAsync(destination.AsMemory(offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("The uploaded file ended unexpectedly.");
            }

            offset += read;
        }
    }
}