import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AlertController, NavController } from '@ionic/angular';
import { AuthService } from '../../auth/auth.service';
import { User } from '../../auth/user.model';
import { UserService } from '../../auth/user.service';
import { ContentReportService } from '../../shared/content-report.service';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.page.html',
  styleUrls: ['./profile.page.scss'],
})
export class ProfilePage implements OnInit {
  user?: User;
  isOwnProfile = false;
  isFollowChanging = false;
  isBlockChanging = false;
  relationshipError = false;
  isReporting = false;
  reportFeedback: string | null = null;
  reportFeedbackIsError = false;
  private viewedUserId: number | null = null;

  constructor(
    private route: ActivatedRoute,
    private authService: AuthService,
    private userService: UserService,
    private contentReportService: ContentReportService,
    private alertController: AlertController,
    private navController: NavController
  ) { }

  ngOnInit() {
    this.route.paramMap.subscribe(paramMap => {
      this.viewedUserId = paramMap.has('userId') ? Number(paramMap.get('userId')) : this.authService.currentUserId;
      this.isOwnProfile = this.viewedUserId === this.authService.currentUserId;
      this.loadUser();
    });
  }

  ionViewWillEnter(): void {
    if (this.user) {
      this.loadUser();
    }
  }

  toggleFollow() {
    if (!this.user || this.isFollowChanging || this.isBlockChanging
      || this.user.isBlockedByCurrentUser || this.user.hasBlockedCurrentUser) {
      return;
    }

    this.isFollowChanging = true;
    this.relationshipError = false;
    const request = this.user.isFollowedByCurrentUser
      ? this.userService.unfollowUser(this.user.id)
      : this.userService.followUser(this.user.id);

    request.subscribe({
      next: () => {
        if (this.user) {
          this.user.isFollowedByCurrentUser = !this.user.isFollowedByCurrentUser;
          this.user.followerCount += this.user.isFollowedByCurrentUser ? 1 : -1;
        }
        this.isFollowChanging = false;
      },
      error: () => {
        this.isFollowChanging = false;
        this.relationshipError = true;
      }
    });
  }

  async confirmBlock(): Promise<void> {
    if (!this.user || this.isBlockChanging) {
      return;
    }

    const alert = await this.alertController.create({
      header: `Block ${this.user.displayName}?`,
      message: 'You will stop following each other, and neither of you can follow the other until you unblock them.',
      buttons: [
        { text: 'Cancel', role: 'cancel' },
        {
          text: 'Block',
          role: 'destructive',
          handler: () => this.blockUser()
        }
      ]
    });
    await alert.present();
  }

  async reportProfile(): Promise<void> {
    const reportedUser = this.user;
    if (!reportedUser || this.isOwnProfile || this.isReporting) {
      return;
    }

    const alert = await this.alertController.create({
      header: `Report ${reportedUser.displayName}'s profile`,
      message: 'Describe the inappropriate content or behavior. Your report will be sent to the site administrators.',
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
    this.contentReportService.reportProfile(reportedUser.id, { description }).subscribe({
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

  unblockUser(): void {
    if (!this.user || this.isBlockChanging) {
      return;
    }

    this.isBlockChanging = true;
    this.relationshipError = false;
    this.userService.unblockUser(this.user.id).subscribe({
      next: () => {
        if (this.user) {
          this.user.isBlockedByCurrentUser = false;
        }
        this.isBlockChanging = false;
      },
      error: () => {
        this.isBlockChanging = false;
        this.relationshipError = true;
      }
    });
  }

  async confirmLogout(): Promise<void> {
    const alert = await this.alertController.create({
      header: 'Log out?',
      message: 'You will need to sign in again to access your account.',
      buttons: [
        { text: 'Cancel', role: 'cancel' },
        {
          text: 'Log out',
          handler: () => this.logout()
        }
      ]
    });
    await alert.present();
  }

  private loadUser(): void {
    if (this.viewedUserId == null) {
      return;
    }

    const request = this.isOwnProfile
      ? this.userService.getCurrentUser()
      : this.userService.getUser(this.viewedUserId);
    request.subscribe(user => this.user = user);
  }

  private blockUser(): void {
    if (!this.user || this.isBlockChanging) {
      return;
    }

    const wasFollowing = this.user.isFollowedByCurrentUser;
    this.isBlockChanging = true;
    this.relationshipError = false;
    this.userService.blockUser(this.user.id).subscribe({
      next: () => {
        if (this.user) {
          this.user.isBlockedByCurrentUser = true;
          this.user.isFollowedByCurrentUser = false;
          if (wasFollowing) {
            this.user.followerCount = Math.max(0, this.user.followerCount - 1);
          }
        }
        this.isBlockChanging = false;
      },
      error: () => {
        this.isBlockChanging = false;
        this.relationshipError = true;
      }
    });
  }

  private logout(): void {
    this.authService.logout();
    void this.navController.navigateRoot('/auth');
  }

}
