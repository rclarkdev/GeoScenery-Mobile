import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { User } from '../auth/user.model';
import { UserService } from '../auth/user.service';
import { Message } from './message.model';
import { MessageService } from './message.service';

@Component({
  selector: 'app-conversation',
  templateUrl: './conversation.page.html',
  styleUrls: ['./conversation.page.scss']
})
export class ConversationPage implements OnInit {
  recipient?: User;
  messages: Message[] = [];
  draft = '';
  isLoading = true;
  isSending = false;
  hasError = false;
  sendError: string | null = null;
  private recipientId: number | null = null;

  constructor(
    private route: ActivatedRoute,
    private authService: AuthService,
    private userService: UserService,
    private messageService: MessageService
  ) { }

  ngOnInit(): void {
    this.recipientId = Number(this.route.snapshot.paramMap.get('userId'));
    this.loadConversation();
  }

  isMine(message: Message): boolean {
    return message.senderId === this.authService.currentUserId;
  }

  handleComposerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  sendMessage(): void {
    const body = this.draft.trim();
    if (!this.recipient || !this.recipientId || !body || this.isSending) {
      return;
    }

    this.isSending = true;
    this.sendError = null;
    this.messageService.sendMessage(this.recipientId, { body }).subscribe({
      next: message => {
        this.messages = [...this.messages, message];
        this.draft = '';
        this.isSending = false;
      },
      error: () => {
        this.sendError = 'Unable to send this message. Check your connection and messaging permissions, then try again.';
        this.isSending = false;
      }
    });
  }

  private loadConversation(): void {
    if (!this.recipientId) {
      this.finishLoadingWithError();
      return;
    }

    this.userService.getUser(this.recipientId).subscribe({
      next: user => {
        this.recipient = user;
        this.messageService.getConversation(this.recipientId as number).subscribe({
          next: messages => {
            this.messages = messages;
            this.isLoading = false;
          },
          error: () => this.finishLoadingWithError()
        });
      },
      error: () => this.finishLoadingWithError()
    });
  }

  private finishLoadingWithError(): void {
    this.hasError = true;
    this.isLoading = false;
  }
}