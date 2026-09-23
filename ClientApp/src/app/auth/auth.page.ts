import { Component } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { NavController } from '@ionic/angular';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-auth',
  templateUrl: './auth.page.html',
  styleUrls: ['./auth.page.scss'],
})
export class AuthPage {
  isRegistering = false;
  isSubmitting = false;
  authError: string | null = null;
  recoverySent = false;
  developmentResetToken: string | null = null;

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
    this.authError = null;
    this.recoverySent = false;
    this.developmentResetToken = null;
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
    this.authError = null;
    const operation = this.isRegistering
      ? this.authService.register(this.authForm.getRawValue())
      : this.authService.login(this.authForm.getRawValue());
    operation.subscribe({
      next: () => this.navController.navigateRoot('/scenery/tabs/observe'),
      error: (error: HttpErrorResponse) => {
        this.isSubmitting = false;
        this.authError = this.getAuthError(error);
      }
    });
  }

  onForgotPassword(): void {
    const email = this.authForm.controls.email;
    if (email.invalid || this.isSubmitting) {
      email.markAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.authError = null;
    this.authService.requestPasswordReset(email.value).subscribe({
      next: response => {
        this.isSubmitting = false;
        this.recoverySent = true;
        this.developmentResetToken = response.developmentToken ?? null;
      },
      error: () => {
        this.isSubmitting = false;
        this.authError = 'Unable to request a password reset right now.';
      }
    });
  }

  private getAuthError(error: HttpErrorResponse): string {
    if (!navigator.onLine || error.status === 0) {
      return 'Unable to reach the server. Check that the API is running and try again.';
    }

    if (this.isRegistering) {
      if (error.status === 409) {
        return 'An account with this email already exists.';
      }
      if (error.status === 429) {
        return 'Too many sign-up attempts. Please try again later.';
      }
      if (error.status === 400) {
        return 'Check your display name, email, and password, then try again.';
      }
      return 'Unable to create the account right now. Please try again.';
    }

    return error.status === 401
      ? 'Unable to authenticate with those details.'
      : 'Unable to sign in right now. Please try again.';
  }
}
