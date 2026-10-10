import { Component } from '@angular/core';
import { Scene } from '../scene.model';
import { SceneryService } from '../scenery.service';

@Component({
  selector: 'app-my-scenes',
  templateUrl: './my-scenes.page.html',
  styleUrls: ['./my-scenes.page.scss'],
})
export class MyScenesPage {

  myScenes: Scene[] = [];
  readonly failedImageIds = new Set<number>();

  constructor(private sceneryService: SceneryService) { }

  ionViewWillEnter(): void {
    this.sceneryService.getMyScenes().subscribe(scenes => {
      this.myScenes = scenes;
      this.failedImageIds.clear();
    });
  }

  onSceneImageError(sceneId: number): void {
    this.failedImageIds.add(sceneId);
  }
}
