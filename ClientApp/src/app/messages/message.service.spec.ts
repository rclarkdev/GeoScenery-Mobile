import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { MessageService } from './message.service';

describe('MessageService', () => {
  let service: MessageService;
  let http: HttpTestingController;
  const messagesUrl = `${environment.apiUrl}/api/messages`;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule]
    });
    service = TestBed.inject(MessageService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('gets conversation summaries', () => {
    service.getConversations().subscribe();
    const request = http.expectOne(messagesUrl);
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('gets a conversation by user', () => {
    service.getConversation(7).subscribe();
    const request = http.expectOne(`${messagesUrl}/7`);
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('sends a message to a user', () => {
    const body = { body: 'Hello from the trail.' };
    service.sendMessage(7, body).subscribe();
    const request = http.expectOne(`${messagesUrl}/7`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    request.flush({ id: 1, senderId: 2, recipientId: 7, ...body, createdAt: '2026-09-30T12:00:00Z' });
  });
});