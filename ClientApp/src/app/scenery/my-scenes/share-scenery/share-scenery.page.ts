import { Component, OnInit } from '@angular/core';
import { Scene } from '../../scene.model';
import { ActivatedRoute } from '@angular/router';
import { NavController } from '@ionic/angular';
import { SceneryService } from '../../scenery.service';
import { AuthService } from '../../../auth/auth.service';

@Component({
  selector: 'app-share-scenery',
  templateUrl: './share-scenery.page.html',
  styleUrls: ['./share-scenery.page.scss'],
})
export class ShareSceneryPage implements OnInit {

  scene?: Scene;
  isLoading = true;
  loadError = false;
  private sceneId?: number;

  constructor(private route: ActivatedRoute, private navCtrl: NavController, private sceneryService: SceneryService, private authService: AuthService) { }

  get canEdit(): boolean {
    return this.scene?.ownerUserId === this.authService.currentUserId;
  }

  ngOnInit() {
    this.route.paramMap.subscribe(paramMap => {
      if (!paramMap.has('sceneId')) {
        this.navCtrl.navigateBack('/scenery/tabs/my-scenes');
        return;
      }
      this.sceneId = Number(paramMap.get('sceneId'));
      if (!Number.isInteger(this.sceneId)) {
        this.navCtrl.navigateBack('/scenery/tabs/my-scenes');
        return;
      }
      this.loadScene();
    });
  }

  retryLoad(): void {
    this.loadScene();
  }

  private loadScene(): void {
    if (this.sceneId == null) {
      return;
    }

    this.isLoading = true;
    this.loadError = false;
    this.scene = undefined;
    this.sceneryService.getScene(this.sceneId).subscribe({
      next: scene => {
        this.scene = scene;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.loadError = true;
      }
    });
  }

}
