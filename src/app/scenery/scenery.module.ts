import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular';
import { SceneryPage } from './scenery.page';
import { SceneryRoutingModule } from './scenery-routing.module';

@NgModule({
  imports: [
    CommonModule,
    IonicModule,
    SceneryRoutingModule
  ],
  declarations: [SceneryPage]
})
export class SceneryPageModule {}
