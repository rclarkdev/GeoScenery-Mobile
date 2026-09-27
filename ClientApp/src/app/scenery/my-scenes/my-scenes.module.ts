import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Routes, RouterModule } from '@angular/router';

import { IonicModule } from '@ionic/angular';

import { MyScenesPage } from './my-scenes.page';
import { ImageUrlPipe } from '../../shared/image-url.pipe';

const routes: Routes = [
  {
    path: '',
    component: MyScenesPage
  }
];

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    ImageUrlPipe,
    RouterModule.forChild(routes)
  ],
  declarations: [MyScenesPage]
})
export class MyScenesPageModule {}
