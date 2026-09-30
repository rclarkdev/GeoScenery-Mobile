import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { MessagesPage } from './messages.page';
import { ConversationPage } from './conversation.page';

const routes: Routes = [
  { path: '', component: MessagesPage },
  { path: ':userId', component: ConversationPage }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class MessagesRoutingModule { }