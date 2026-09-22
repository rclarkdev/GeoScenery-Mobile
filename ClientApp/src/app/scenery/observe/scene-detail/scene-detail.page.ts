import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { NavController } from '@ionic/angular';
import { AuthService } from '../../../auth/auth.service';
import { SceneryService } from '../../scenery.service';
import { Scene } from '../../scene.model';
import { VisitsService } from '../../../visits/visits.service';

@Component({
  selector: 'app-scene-detail',
  templateUrl: './scene-detail.page.html',
  styleUrls: ['./scene-detail.page.scss'],
})
export class SceneDetailPage implements OnInit {

  scene?: Scene;
  isVisiting = false;
  visitError = false;
  pendingRating: number | null = null;
  isRating = false;
  ratingError = false;

  constructor(
    private navCtrl: NavController,
    private route: ActivatedRoute,
    private authService: AuthService,
    private sceneryService: SceneryService,
    private visitsService: VisitsService) { }

  ngOnInit() {
    this.route.paramMap.subscribe(paramMap => {
      if (!paramMap.has('sceneId')) {
        this.navCtrl.navigateBack('/scenery/tabs/observe');
      }
      const sceneId = Number(paramMap.get('sceneId'));
      if (!Number.isInteger(sceneId)) {
        this.navCtrl.navigateBack('/scenery/tabs/observe');
        return;
      }
      this.sceneryService.getScene(sceneId).subscribe(scene => {
        this.scene = scene;
        this.pendingRating = scene.currentUserRating ?? null;
      });
    });
  }

  get isOwnScene(): boolean {
    return !!this.scene && this.scene.ownerUserId === this.authService.currentUserId;
  }

  onVisitScene() {
    if (!this.scene || this.isVisiting) {
      return;
    }

    this.isVisiting = true;
    this.visitError = false;
    this.visitsService.recordVisit(this.scene.id).subscribe({
      next: () => this.navCtrl.navigateBack('/visits'),
      error: () => {
        this.isVisiting = false;
        this.visitError = true;
      }
    });
  }

  onSubmitRating() {
    if (!this.scene || this.isRating || this.pendingRating == null) {
      return;
    }

    this.isRating = true;
    this.ratingError = false;
    this.sceneryService.rateScene(this.scene.id, this.pendingRating).subscribe({
      next: scene => {
        this.scene = scene;
        this.isRating = false;
      },
      error: () => {
        this.isRating = false;
        this.ratingError = true;
      }
    });
  }

  onRemoveRating() {
    if (!this.scene || this.isRating || this.scene.currentUserRating == null) {
      return;
    }

    this.isRating = true;
    this.ratingError = false;
    this.sceneryService.removeRating(this.scene.id).subscribe({
      next: () => {
        if (this.scene) {
          this.scene.currentUserRating = undefined;
        }
        this.pendingRating = null;
        this.isRating = false;
      },
      error: () => {
        this.isRating = false;
        this.ratingError = true;
      }
    });
  }
}
