import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { Routes, RouterModule } from '@angular/router';

import { IonicModule } from '@ionic/angular';

import { AuthPage } from './auth.page';
import { ResetPasswordPage } from './reset-password.page';

const routes: Routes = [
  {
    path: '',
    component: AuthPage
  },
  {
    path: 'reset-password',
    component: ResetPasswordPage
  }
];

@NgModule({
  imports: [
    CommonModule,
    ReactiveFormsModule,
    IonicModule,
    RouterModule.forChild(routes)
  ],
  declarations: [AuthPage, ResetPasswordPage]
})
export class AuthPageModule {}
