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
  registrationComplete = false;
  developmentVerificationToken: string | null = null;
  verificationResent = false;
  verificationError: string | null = null;
  canResendVerification = false;

  readonly authForm = this.formBuilder.nonNullable.group({
    displayName: [''],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirmPassword: ['']
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
    this.registrationComplete = false;
    this.developmentVerificationToken = null;
    this.verificationResent = false;
    this.verificationError = null;
    this.canResendVerification = false;
    const displayName = this.authForm.controls.displayName;
    displayName.reset();
    if (this.isRegistering) {
      displayName.addValidators(Validators.required);
    } else {
      displayName.removeValidators(Validators.required);
    }
    displayName.updateValueAndValidity();
    const confirmPassword = this.authForm.controls.confirmPassword;
    confirmPassword.reset();
    if (this.isRegistering) {
      confirmPassword.setValidators(Validators.required);
    } else {
      confirmPassword.clearValidators();
    }
    confirmPassword.updateValueAndValidity();
  }

  onSubmit(): void {
    if (this.authForm.invalid || this.isSubmitting) {
      this.authForm.markAllAsTouched();
      return;
    }
    this.isSubmitting = true;
    this.authError = null;
    if (this.isRegistering) {
      const { displayName, email, password, confirmPassword } = this.authForm.getRawValue();
      if (password !== confirmPassword) {
        this.isSubmitting = false;
        this.authError = 'Passwords do not match.';
        return;
      }
      this.authService.register({ displayName, email, password, confirmPassword }).subscribe({
        next: response => {
          this.isSubmitting = false;
          this.registrationComplete = true;
          this.developmentVerificationToken = response.developmentToken ?? null;
        },
        error: (error: HttpErrorResponse) => {
          this.isSubmitting = false;
          this.authError = this.getAuthError(error);
        }
      });
      return;
    }

    this.authService.login({
      email: this.authForm.controls.email.value,
      password: this.authForm.controls.password.value
    }).subscribe({
      next: () => this.navController.navigateRoot('/scenery/tabs/profile'),
      error: (error: HttpErrorResponse) => {
        this.isSubmitting = false;
        this.authError = this.getAuthError(error);
        this.canResendVerification = error.status === 403;
      }
    });
  }

  onResendVerification(): void {
    const email = this.authForm.controls.email;
    if (email.invalid || this.isSubmitting) {
      email.markAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.verificationError = null;
    this.authService.resendVerification(email.value).subscribe({
      next: response => {
        this.isSubmitting = false;
        this.verificationResent = true;
        this.developmentVerificationToken = response.developmentToken ?? this.developmentVerificationToken;
      },
      error: () => {
        this.isSubmitting = false;
        this.verificationError = 'Unable to request a verification email right now. Please try again later.';
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
        return 'Check your display name, email, password, and password confirmation, then try again.';
      }
      return 'Unable to create the account right now. Please try again.';
    }

    if (error.status === 403) {
      return 'Please verify your email address before signing in. You can request a new verification email below.';
    }

    return error.status === 401
      ? 'Unable to authenticate with those details.'
      : 'Unable to sign in right now. Please try again.';
  }
}
