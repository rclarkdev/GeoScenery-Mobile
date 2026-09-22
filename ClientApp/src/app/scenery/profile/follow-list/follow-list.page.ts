import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { UserSummary } from '../../../auth/user-summary.model';
import { UserService } from '../../../auth/user.service';

@Component({
  selector: 'app-follow-list',
  templateUrl: './follow-list.page.html',
  styleUrls: ['./follow-list.page.scss'],
})
export class FollowListPage implements OnInit {
  users: UserSummary[] = [];
  title = 'Followers';

  constructor(
    private route: ActivatedRoute,
    private userService: UserService
  ) { }

  ngOnInit() {
    this.route.paramMap.subscribe(paramMap => {
      const userId = Number(paramMap.get('userId'));
      const isFollowers = this.route.snapshot.data['mode'] !== 'following';
      this.title = isFollowers ? 'Followers' : 'Following';
      const request = isFollowers ? this.userService.getFollowers(userId) : this.userService.getFollowing(userId);
      request.subscribe(users => this.users = users);
    });
  }
}
