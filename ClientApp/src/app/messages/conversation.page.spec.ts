import { CommonModule } from '@angular/common';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { IonicModule } from '@ionic/angular';
import { of } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { User } from '../auth/user.model';
import { UserService } from '../auth/user.service';
import { ConversationPage } from './conversation.page';
import { MessageService } from './message.service';

describe('ConversationPage', () => {
  let fixture: ComponentFixture<ConversationPage>;
  let userService: jasmine.SpyObj<UserService>;
  let messageService: jasmine.SpyObj<MessageService>;

  beforeEach(() => {
    userService = jasmine.createSpyObj<UserService>('UserService', ['getUser']);
    userService.getUser.and.returnValue(of({ id: 2, displayName: 'Follower', canMessage: false } as User));

    messageService = jasmine.createSpyObj<MessageService>('MessageService', ['getConversation', 'sendMessage']);
    messageService.getConversation.and.returnValue(of([]));

    TestBed.configureTestingModule({
      imports: [CommonModule, FormsModule, IonicModule],
      declarations: [ConversationPage],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => '2' } } } },
        { provide: AuthService, useValue: { currentUserId: 1 } },
        { provide: UserService, useValue: userService },
        { provide: MessageService, useValue: messageService }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    });

    fixture = TestBed.createComponent(ConversationPage);
    fixture.detectChanges();
  });

  it('shows the message editor when the recipient capability flag is false', () => {
    expect(fixture.nativeElement.querySelector('[aria-label="Message"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('[aria-label="Send message"]')).not.toBeNull();
  });
});
