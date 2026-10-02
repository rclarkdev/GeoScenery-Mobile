import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { forkJoin } from 'rxjs';
import { User } from '../../auth/user.model';
import { UserService } from '../../auth/user.service';
import { Scene } from '../scene.model';
import { SceneryService } from '../scenery.service';

@Component({
  selector: 'app-profile-scenes',
  templateUrl: './profile-scenes.page.html',
  styleUrls: ['./profile-scenes.page.scss']
})
export class ProfileScenesPage implements OnInit {
  user?: User;
  scenes: Scene[] = [];
  isLoading = true;
  loadError = false;

  constructor(
    private route: ActivatedRoute,
    private userService: UserService,
    private sceneryService: SceneryService
  ) { }

  ngOnInit(): void {
    this.route.paramMap.subscribe(paramMap => {
      const userId = Number(paramMap.get('userId'));
      if (!Number.isInteger(userId) || userId < 1) {
        this.isLoading = false;
        this.loadError = true;
        return;
      }

      this.isLoading = true;
      this.loadError = false;
      forkJoin({
        user: this.userService.getUser(userId),
        scenes: this.sceneryService.getUserPublicScenes(userId)
      }).subscribe({
        next: result => {
          this.user = result.user;
          this.scenes = result.scenes;
          this.isLoading = false;
        },
        error: () => {
          this.isLoading = false;
          this.loadError = true;
        }
      });
    });
  }
}