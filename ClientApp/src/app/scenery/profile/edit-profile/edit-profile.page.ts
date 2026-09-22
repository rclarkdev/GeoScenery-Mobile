import { Component, OnInit } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { NavController } from '@ionic/angular';
import { AuthService } from '../../../auth/auth.service';
import { User } from '../../../auth/user.model';
import { UserService } from '../../../auth/user.service';

@Component({
  selector: 'app-edit-profile',
  templateUrl: './edit-profile.page.html',
  styleUrls: ['./edit-profile.page.scss'],
})
export class EditProfilePage implements OnInit {
  user?: User;
  isSaving = false;
  saveError = false;

  readonly profileForm = this.formBuilder.nonNullable.group({
    displayName: ['', [Validators.required, Validators.maxLength(200)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(320)]],
    birthDate: this.formBuilder.control<string | null>(null),
    education: this.formBuilder.control<string | null>(null, [Validators.maxLength(200)]),
    hobbies: this.formBuilder.control<string | null>(null, [Validators.maxLength(500)]),
    employment: this.formBuilder.control<string | null>(null, [Validators.maxLength(200)]),
    bio: this.formBuilder.control<string | null>(null, [Validators.maxLength(2000)])
  });

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private userService: UserService,
    private navCtrl: NavController
  ) { }

  ngOnInit() {
    this.userService.getCurrentUser().subscribe(user => {
      this.user = user;
      this.profileForm.setValue({
        displayName: user.displayName,
        email: user.email ?? '',
        birthDate: user.birthDate ?? null,
        education: user.education ?? null,
        hobbies: user.hobbies ?? null,
        employment: user.employment ?? null,
        bio: user.bio ?? null
      });
    });
  }

  onSave() {
    if (!this.user || this.profileForm.invalid || this.isSaving) {
      this.profileForm.markAllAsTouched();
      return;
    }

    this.isSaving = true;
    this.saveError = false;
    this.userService.updateUser(this.user.id, {
      ...this.profileForm.getRawValue(),
      profileImageUrl: this.user.profileImageUrl,
      latitude: this.user.latitude,
      longitude: this.user.longitude
    }).subscribe({
      next: () => this.navCtrl.navigateBack('/scenery/tabs/profile'),
      error: () => {
        this.isSaving = false;
        this.saveError = true;
      }
    });
  }
}
