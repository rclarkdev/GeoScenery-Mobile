import { Component, OnInit } from '@angular/core';
import { ConversationSummary } from './message.model';
import { MessageService } from './message.service';

@Component({
  selector: 'app-messages',
  templateUrl: './messages.page.html',
  styleUrls: ['./messages.page.scss']
})
export class MessagesPage implements OnInit {
  conversations: ConversationSummary[] = [];
  isLoading = true;
  hasError = false;

  constructor(private messageService: MessageService) { }

  ngOnInit(): void {
    this.loadConversations();
  }

  ionViewWillEnter(): void {
    this.loadConversations();
  }

  private loadConversations(): void {
    this.isLoading = true;
    this.hasError = false;
    this.messageService.getConversations().subscribe({
      next: conversations => {
        this.conversations = conversations;
        this.isLoading = false;
      },
      error: () => {
        this.hasError = true;
        this.isLoading = false;
      }
    });
  }
}