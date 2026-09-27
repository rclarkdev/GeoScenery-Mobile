import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { Routes, RouterModule } from '@angular/router';

import { IonicModule } from '@ionic/angular';

import { EditScenePage } from './edit-scene.page';
import { ImageUrlPipe } from '../../../shared/image-url.pipe';

const routes: Routes = [
  {
    path: '',
    component: EditScenePage
  }
];

@NgModule({
  imports: [
    CommonModule,
    ReactiveFormsModule,
    IonicModule,
    ImageUrlPipe,
    RouterModule.forChild(routes)
  ],
  declarations: [EditScenePage]
})
export class EditScenePageModule {}
