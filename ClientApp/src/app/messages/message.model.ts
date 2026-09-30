import { UserSummary } from '../auth/user-summary.model';

export interface Message {
  id: number;
  senderId: number;
  recipientId: number;
  body: string;
  createdAt: string;
}

export interface ConversationSummary {
  user: UserSummary;
  lastMessage: Message;
  unreadCount: number;
}

export interface SendMessageRequest {
  body: string;
}