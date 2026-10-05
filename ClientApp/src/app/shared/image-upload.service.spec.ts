import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ImageUploadService } from './image-upload.service';
import { environment } from '../../environments/environment';

describe('ImageUploadService', () => {
  let service: ImageUploadService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [HttpClientTestingModule] });
    service = TestBed.inject(ImageUploadService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('uploads a cropped Blob directly as multipart form data without converting it to a data URL', () => {
    const croppedFile = new File(['cropped bytes'], 'cropped-photo.jpg', { type: 'image/jpeg' });
    let uploadedUrl: string | undefined;

    service.uploadSelectedImage(croppedFile, 'scene').subscribe(response => uploadedUrl = response.url);

    const request = httpMock.expectOne(`${environment.apiUrl}/api/images`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body instanceof FormData).toBeTrue();
    expect((request.request.body as FormData).get('kind')).toBe('scene');
    expect((request.request.body as FormData).get('file')).toEqual(jasmine.any(File));
    expect(((request.request.body as FormData).get('file') as File).name).toBe('photo.jpg');
    request.flush({ url: '/uploads/cropped.jpg' });

    expect(uploadedUrl).toBe('/uploads/cropped.jpg');
  });

  it('rejects unsupported Blob content before making a request', () => {
    expect(() => service.uploadSelectedImage(new Blob(['bad'], { type: 'image/gif' }), 'profile'))
      .toThrowError('Choose a JPEG, PNG, or WebP image.');
    httpMock.expectNone(`${environment.apiUrl}/api/images`);
  });
});
