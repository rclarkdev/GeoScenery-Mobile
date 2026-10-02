import { CUSTOM_ELEMENTS_SCHEMA, NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Routes, RouterModule } from '@angular/router';

import { IonicModule } from '@ionic/angular';

import { ProfilePage } from './profile.page';
import { FollowListPage } from './follow-list/follow-list.page';
import { EditProfilePage } from './edit-profile/edit-profile.page';
import { ProfileScenesPage } from './profile-scenes.page';
import { ImageUrlPipe } from '../../shared/image-url.pipe';

const routes: Routes = [
  {
    path: ':userId/scenes',
    component: ProfileScenesPage
  },
  {
    path: '',
    component: ProfilePage
  },
  {
    path: 'edit',
    component: EditProfilePage
  },
  {
    path: ':userId',
    component: ProfilePage
  },
  {
    path: ':userId/followers',
    component: FollowListPage,
    data: { mode: 'followers' }
  },
  {
    path: ':userId/following',
    component: FollowListPage,
    data: { mode: 'following' }
  }
];

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    IonicModule,
    ImageUrlPipe,
    RouterModule.forChild(routes)
  ],
  declarations: [ProfilePage, ProfileScenesPage, FollowListPage, EditProfilePage],
  schemas: [CUSTOM_ELEMENTS_SCHEMA]
})
export class ProfilePageModule {}

