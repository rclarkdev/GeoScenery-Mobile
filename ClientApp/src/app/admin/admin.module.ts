import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular';
import { AdminPage } from './admin.page';
import { AdminRoutingModule } from './admin-routing.module';

@NgModule({
  imports: [CommonModule, FormsModule, IonicModule, AdminRoutingModule],
  declarations: [AdminPage]
})
export class AdminModule { }
