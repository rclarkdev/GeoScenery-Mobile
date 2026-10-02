import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface SupportContactRequest {
  name: string;
  email: string;
  topic: string;
  message: string;
  website?: string;
}

export interface SupportContactResponse {
  message: string;
}

@Injectable({ providedIn: 'root' })
export class SupportContactService {
  private readonly contactUrl = `${environment.apiUrl}/api/support/contact`;

  constructor(private http: HttpClient) { }

  send(request: SupportContactRequest): Observable<SupportContactResponse> {
    return this.http.post<SupportContactResponse>(this.contactUrl, request);
  }
}
