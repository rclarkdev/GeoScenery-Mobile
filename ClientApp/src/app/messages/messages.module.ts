import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular';
import { ImageUrlPipe } from '../shared/image-url.pipe';
import { ConversationPage } from './conversation.page';
import { MessagesPage } from './messages.page';
import { MessagesRoutingModule } from './messages-routing.module';

@NgModule({
  imports: [CommonModule, FormsModule, IonicModule, ImageUrlPipe, MessagesRoutingModule],
  declarations: [MessagesPage, ConversationPage]
})
export class MessagesModule { }