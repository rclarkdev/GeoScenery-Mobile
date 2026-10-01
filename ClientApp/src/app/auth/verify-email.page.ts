import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from './auth.service';

@Component({
  selector: 'app-verify-email',
  templateUrl: './verify-email.page.html'
})
export class VerifyEmailPage implements OnInit {
  isVerifying = true;
  verified = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private authService: AuthService
  ) { }

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!token) {
      this.isVerifying = false;
      return;
    }

    this.authService.verifyEmail(token).subscribe({
      next: () => {
        this.isVerifying = false;
        this.verified = true;
      },
      error: () => this.isVerifying = false
    });
  }

  backToSignIn(): void {
    void this.router.navigateByUrl('/auth');
  }
}
