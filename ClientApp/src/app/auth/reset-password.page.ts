import { Component } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-reset-password',
  templateUrl: './reset-password.page.html'
})
export class ResetPasswordPage {
  isSubmitting = false;
  completed = false;
  resetError = false;

  readonly resetForm = this.formBuilder.nonNullable.group({
    password: ['', [Validators.required, Validators.minLength(8)]],
    confirmation: ['', [Validators.required]]
  });

  constructor(
    private formBuilder: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private authService: AuthService
  ) { }

  onSubmit(): void {
    if (this.resetForm.invalid || this.resetForm.value.password !== this.resetForm.value.confirmation || this.isSubmitting) {
      this.resetForm.markAllAsTouched();
      return;
    }

    const token = this.route.snapshot.queryParamMap.get('token');
    if (!token) {
      this.resetError = true;
      return;
    }

    this.isSubmitting = true;
    this.resetError = false;
    this.authService.resetPassword(token, this.resetForm.getRawValue().password).subscribe({
      next: () => { this.isSubmitting = false; this.completed = true; },
      error: () => { this.isSubmitting = false; this.resetError = true; }
    });
  }

  backToLogin(): void {
    void this.router.navigateByUrl('/auth');
  }
}