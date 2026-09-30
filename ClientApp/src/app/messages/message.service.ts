import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import { ConversationSummary, Message, SendMessageRequest } from './message.model';

@Injectable({
  providedIn: 'root'
})
export class MessageService {
  private readonly messagesUrl = `${environment.apiUrl}/api/messages`;

  constructor(private http: HttpClient) { }

  getConversations(): Observable<ConversationSummary[]> {
    return this.http.get<ConversationSummary[]>(this.messagesUrl);
  }

  getConversation(userId: number): Observable<Message[]> {
    return this.http.get<Message[]>(`${this.messagesUrl}/${userId}`);
  }

  sendMessage(userId: number, request: SendMessageRequest): Observable<Message> {
    return this.http.post<Message>(`${this.messagesUrl}/${userId}`, request);
  }
}