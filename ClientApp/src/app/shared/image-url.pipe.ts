import { Pipe, PipeTransform } from '@angular/core';
import { environment } from '../../environments/environment';

@Pipe({
  name: 'imageUrl',
  standalone: true
})
export class ImageUrlPipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value || /^(blob:|data:|https?:)/.test(value)) {
      return value ?? '';
    }

    return new URL(value, `${environment.apiUrl}/`).toString();
  }
}