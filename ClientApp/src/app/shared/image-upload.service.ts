import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom, from, Observable } from 'rxjs';
import { environment } from '../../environments/environment';

interface UploadedImageResponse {
  url: string;
}

@Injectable({ providedIn: 'root' })
export class ImageUploadService {
  private readonly imagesUrl = `${environment.apiUrl}/api/images`;

  constructor(private http: HttpClient) { }

  uploadDataUrl(dataUrl: string, kind: 'scene' | 'profile'): Observable<UploadedImageResponse> {
    const [metadata, encoded] = dataUrl.split(',', 2);
    if (!metadata?.startsWith('data:image/') || !encoded) {
      throw new Error('The selected image is invalid.');
    }

    const mimeType = metadata.slice(5, metadata.indexOf(';'));
    const bytes = Uint8Array.from(atob(encoded), character => character.charCodeAt(0));
    const formData = new FormData();
    formData.append('file', new Blob([bytes], { type: mimeType }), 'capture');
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
      const formData = new FormData();
      formData.append('file', blob, 'capture');
      formData.append('kind', kind);
      return firstValueFrom(this.http.post<UploadedImageResponse>(this.imagesUrl, formData));
    }).then(response => response));
  }
}
