# GeoScenery Mobile Client

## Photo cropping

Scene and profile photos are cropped in the reusable Ionic photo editor before upload. Web users choose a JPEG, PNG, or WebP file; native users keep the Camera plugin's camera/gallery prompt. Cropped files remain local until the scene or profile is saved, then upload as multipart `Blob` data through `ImageUploadService`.

Scene crops default to 16:9 and offer 4:3, square, and free proportions. Profile crops use a circular preview and square output. The crop editor limits the longest output side to 1600 pixels for scenes and 512 pixels for profiles; PNG input remains PNG to preserve transparency, and generated output over the server's 5 MiB limit must be cropped smaller before saving. Server-side image validation and resizing remain authoritative.
