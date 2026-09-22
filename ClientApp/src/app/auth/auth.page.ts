import { Component } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { NavController } from '@ionic/angular';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-auth',
  templateUrl: './auth.page.html',
  styleUrls: ['./auth.page.scss'],
})
export class AuthPage {
  isRegistering = false;
  isSubmitting = false;
  authError = false;

  readonly authForm = this.formBuilder.nonNullable.group({
    displayName: [''],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]]
  });

  constructor(
    private formBuilder: FormBuilder,
    private authService: AuthService,
    private navController: NavController
  ) { }

  toggleMode(): void {
    this.isRegistering = !this.isRegistering;
    this.authError = false;
    const displayName = this.authForm.controls.displayName;
    displayName.reset();
    if (this.isRegistering) {
      displayName.addValidators(Validators.required);
    } else {
      displayName.removeValidators(Validators.required);
    }
    displayName.updateValueAndValidity();
  }

  onSubmit(): void {
    if (this.authForm.invalid || this.isSubmitting) {
      this.authForm.markAllAsTouched();
      return;
    }
    this.isSubmitting = true;
    this.authError = false;
    const operation = this.isRegistering
      ? this.authService.register(this.authForm.getRawValue())
      : this.authService.login(this.authForm.getRawValue());
    operation.subscribe({
      next: () => this.navController.navigateRoot('/scenery/tabs/observe'),
      error: () => { this.isSubmitting = false; this.authError = true; }
    });
  }
}
