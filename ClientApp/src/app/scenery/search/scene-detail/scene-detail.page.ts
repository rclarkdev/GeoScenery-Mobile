import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AlertController, NavController } from '@ionic/angular';
import { AuthService } from '../../../auth/auth.service';
import { User } from '../../../auth/user.model';
import { UserService } from '../../../auth/user.service';
import { SceneryService } from '../../scenery.service';
import { Scene } from '../../scene.model';
import { VisitsService } from '../../../visits/visits.service';
import { ContentReportService } from '../../../shared/content-report.service';

@Component({
  selector: 'app-scene-detail',
  templateUrl: './scene-detail.page.html',
  styleUrls: ['./scene-detail.page.scss'],
})
export class SceneDetailPage implements OnInit {

  scene?: Scene;
  owner?: User;
  isLoading = true;
  loadError = false;
  isVisiting = false;
  visitError = false;
  visitRecorded = false;
  pendingRating: number | null = null;
  isRating = false;
  ratingError = false;
  isReporting = false;
  reportFeedback: string | null = null;
  reportFeedbackIsError = false;
  private sceneId?: number;

  constructor(
    private navCtrl: NavController,
    private route: ActivatedRoute,
    private authService: AuthService,
    private userService: UserService,
    private sceneryService: SceneryService,
    private visitsService: VisitsService,
    private alertController: AlertController,
    private contentReportService: ContentReportService) { }

  ngOnInit() {
    this.route.paramMap.subscribe(paramMap => {
      if (!paramMap.has('sceneId')) {
        this.navCtrl.navigateBack('/scenery/tabs/search');
        return;
      }
      this.sceneId = Number(paramMap.get('sceneId'));
      if (!Number.isInteger(this.sceneId)) {
        this.navCtrl.navigateBack('/scenery/tabs/search');
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
    this.owner = undefined;
    this.sceneryService.getScene(this.sceneId).subscribe({
      next: scene => {
        this.scene = scene;
        this.pendingRating = scene.currentUserRating ?? null;
        this.isLoading = false;
        if (scene.ownerUserId != null) {
          this.userService.getUser(scene.ownerUserId).subscribe({
            next: owner => this.owner = owner,
            error: () => this.owner = undefined
          });
        }
      },
      error: () => {
        this.isLoading = false;
        this.loadError = true;
      }
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
      next: () => {
        this.isVisiting = false;
        this.visitRecorded = true;
      },
      error: () => {
        this.isVisiting = false;
        this.visitError = true;
      }
    });
  }

  onSubmitRating() {
    if (!this.scene || this.isRating || this.pendingRating == null
      || this.pendingRating < 0 || this.pendingRating > 10) {
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

  async reportScene(): Promise<void> {
    const reportedScene = this.scene;
    if (!reportedScene || this.isOwnScene || this.isReporting) {
      return;
    }

    const alert = await this.alertController.create({
      header: `Report ${reportedScene.title}`,
      message: 'Describe the inappropriate content. Your report will be sent to the site administrators.',
      inputs: [{
        name: 'description',
        type: 'textarea',
        placeholder: 'Describe the violation...',
        attributes: { maxlength: 2000, rows: 5 }
      }],
      buttons: [
        { text: 'Cancel', role: 'cancel' },
        { text: 'Submit report', role: 'confirm' }
      ]
    });
    await alert.present();
    const result = await alert.onDidDismiss();
    if (result.role !== 'confirm') {
      return;
    }

    const description = String(result.data?.values?.description ?? '').trim();
    if (!description) {
      this.reportFeedback = 'Enter a description of the violation before submitting.';
      this.reportFeedbackIsError = true;
      return;
    }

    this.isReporting = true;
    this.reportFeedback = null;
    this.contentReportService.reportScene(reportedScene.id, { description }).subscribe({
      next: () => {
        this.isReporting = false;
        this.reportFeedback = 'Report submitted. Thank you for helping keep GeoScenery safe.';
        this.reportFeedbackIsError = false;
      },
      error: () => {
        this.isReporting = false;
        this.reportFeedback = 'Unable to submit your report. Please try again.';
        this.reportFeedbackIsError = true;
      }
    });
  }
}
