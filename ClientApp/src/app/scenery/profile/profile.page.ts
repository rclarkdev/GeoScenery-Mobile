import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { AlertController, NavController } from '@ionic/angular';
import { AuthService } from '../../auth/auth.service';
import { User } from '../../auth/user.model';
import { UserService } from '../../auth/user.service';

@Component({
  selector: 'app-profile',
  templateUrl: './profile.page.html',
  styleUrls: ['./profile.page.scss'],
})
export class ProfilePage implements OnInit {
  user?: User;
  isOwnProfile = false;
  isFollowChanging = false;
  private viewedUserId: number | null = null;

  constructor(
    private route: ActivatedRoute,
    private authService: AuthService,
    private userService: UserService,
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
    if (!this.user || this.isFollowChanging) {
      return;
    }

    this.isFollowChanging = true;
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
      error: () => this.isFollowChanging = false
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

  private logout(): void {
    this.authService.logout();
    void this.navController.navigateRoot('/auth');
  }

}
