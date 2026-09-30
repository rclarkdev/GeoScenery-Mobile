import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Routes, RouterModule } from '@angular/router';

import { IonicModule } from '@ionic/angular';

import { SceneDetailPage } from './scene-detail.page';
import { ImageUrlPipe } from '../../../shared/image-url.pipe';

const routes: Routes = [
  {
    path: '',
    component: SceneDetailPage
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
  declarations: [SceneDetailPage]
})
export class SceneDetailPageModule {}
