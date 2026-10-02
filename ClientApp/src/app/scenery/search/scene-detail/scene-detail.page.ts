import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AlertController, NavController } from '@ionic/angular';
import { AuthService } from '../../../auth/auth.service';
import { User } from '../../../auth/user.model';
import { UserService } from '../../../auth/user.service';
import { SceneryService } from '../../scenery.service';
import { Scene } from '../../scene.model';
import { Visit } from '../../../visits/visit.model';
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
  isRemovingVisit = false;
  isCheckingVisit = true;
  visitError = false;
  visitRecorded = false;
  private visitId?: number;
  pendingRating: number | null = null;
  ratingDescription = '';
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
    this.visitId = undefined;
    this.visitRecorded = false;
    this.isCheckingVisit = true;
    this.sceneryService.getScene(this.sceneId).subscribe({
      next: scene => {
        this.scene = scene;
        this.pendingRating = scene.currentUserRating ?? null;
        this.ratingDescription = scene.currentUserRatingDescription ?? '';
        this.isLoading = false;
        if (scene.ownerUserId != null) {
          this.userService.getUser(scene.ownerUserId).subscribe({
            next: owner => this.owner = owner,
            error: () => this.owner = undefined
          });
        }

        if (this.isOwnScene) {
          this.isCheckingVisit = false;
          return;
        }

        this.visitsService.getUserVisits().subscribe({
          next: visits => {
            this.setVisitState(visits.find(visit => visit.sceneId === scene.id));
            this.isCheckingVisit = false;
          },
          error: () => this.isCheckingVisit = false
        });
      },
      error: () => {
        this.isLoading = false;
        this.loadError = true;
      }
    });
  }

  get visitButtonLabel(): string {
    if (this.isCheckingVisit) {
      return 'Checking visit...';
    }
    if (this.isRemovingVisit) {
      return 'Removing visit...';
    }
    if (this.isVisiting) {
      return 'Saving...';
    }
    return this.visitRecorded ? 'Unvisit' : 'Mark as visited';
  }

  get isOwnScene(): boolean {
    return !!this.scene && this.scene.ownerUserId === this.authService.currentUserId;
  }

  onVisitScene() {
    if (this.visitRecorded) {
      this.removeVisit();
      return;
    }
    if (!this.scene || this.isVisiting || this.isCheckingVisit) {
      return;
    }

    this.isVisiting = true;
    this.visitError = false;
    this.visitsService.recordVisit(this.scene.id).subscribe({
      next: visit => {
        this.isVisiting = false;
        this.setVisitState(visit);
      },
      error: () => {
        this.isVisiting = false;
        this.visitError = true;
      }
    });
  }

  private removeVisit(): void {
    if (this.visitId == null || this.isRemovingVisit) {
      return;
    }

    this.isRemovingVisit = true;
    this.visitError = false;
    this.visitsService.removeVisit(this.visitId).subscribe({
      next: () => {
        this.isRemovingVisit = false;
        this.setVisitState(undefined);
      },
      error: () => {
        this.isRemovingVisit = false;
        this.visitError = true;
      }
    });
  }

  private setVisitState(visit?: Visit): void {
    this.visitId = visit?.id;
    this.visitRecorded = visit != null;
  }

  onSubmitRating() {
    if (!this.scene || this.isRating || this.pendingRating == null
      || this.pendingRating < 0 || this.pendingRating > 10) {
      return;
    }

    this.isRating = true;
    this.ratingError = false;
    this.sceneryService.rateScene(this.scene.id, this.pendingRating, this.ratingDescription.trim()).subscribe({
      next: scene => {
        this.scene = scene;
        this.pendingRating = scene.currentUserRating ?? null;
        this.ratingDescription = scene.currentUserRatingDescription ?? '';
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
          this.scene.currentUserRatingDescription = undefined;
        }
        this.pendingRating = null;
        this.ratingDescription = '';
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
