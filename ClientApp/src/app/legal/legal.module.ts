import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular';
import { RouterModule, Routes } from '@angular/router';
import { LegalPage } from './legal.page';

const routes: Routes = [{ path: ':document', component: LegalPage }];

@NgModule({
  imports: [CommonModule, IonicModule, RouterModule.forChild(routes)],
  declarations: [LegalPage]
})
export class LegalPageModule {}