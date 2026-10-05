import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, from, Observable, of } from 'rxjs';
import { environment } from '../../environments/environment';

interface UploadedImageResponse {
  url: string;
}

@Injectable({ providedIn: 'root' })
export class ImageUploadService {
  private readonly imagesUrl = `${environment.apiUrl}/api/images`;

  constructor(private http: HttpClient) { }

  uploadSelectedImage(source: string | Blob, kind: 'scene' | 'profile'): Observable<UploadedImageResponse> {
    if (source instanceof Blob) {
      return this.uploadBlob(source, kind);
    }

    if (source.startsWith('/uploads/')) {
      return of({ url: source });
    }

    return source.startsWith('data:')
      ? this.uploadDataUrl(source, kind)
      : this.uploadUri(source, kind);
  }

  uploadDataUrl(dataUrl: string, kind: 'scene' | 'profile'): Observable<UploadedImageResponse> {
    const [metadata, encoded] = dataUrl.split(',', 2);
    if (!metadata?.startsWith('data:image/') || !encoded) {
      throw new Error('The selected image is invalid.');
    }

    const mimeType = metadata.slice(5, metadata.indexOf(';')).toLowerCase();
    if (!this.isSupportedImageType(mimeType)) {
      throw new Error('Choose a JPEG, PNG, or WebP image.');
    }
    const bytes = Uint8Array.from(atob(encoded), character => character.charCodeAt(0));
    return this.uploadBlob(new Blob([bytes], { type: mimeType }), kind);
  }

  uploadBlob(blob: Blob, kind: 'scene' | 'profile'): Observable<UploadedImageResponse> {
    if (!this.isSupportedImageType(blob.type.toLowerCase())) {
      throw new Error('Choose a JPEG, PNG, or WebP image.');
    }

    const extension = blob.type.toLowerCase() === 'image/png'
      ? 'png'
      : blob.type.toLowerCase() === 'image/webp' ? 'webp' : 'jpg';
    const formData = new FormData();
    formData.append('file', blob, `photo.${extension}`);
    formData.append('kind', kind);
    return this.http.post<UploadedImageResponse>(this.imagesUrl, formData);
  }

  uploadUri(uri: string, kind: 'scene' | 'profile'): Observable<UploadedImageResponse> {
    return from(fetch(uri).then(response => {
      if (!response.ok) {
        throw new Error('The selected image could not be read.');
      }
      return response.blob();
    }).then(blob => {
      return firstValueFrom(this.uploadBlob(blob, kind));
    }).then(response => response));
  }

  private isSupportedImageType(mimeType: string): boolean {
    return ['image/jpeg', 'image/png', 'image/webp'].includes(mimeType);
  }
}
