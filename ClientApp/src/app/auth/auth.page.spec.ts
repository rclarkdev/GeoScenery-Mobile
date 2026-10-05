import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { IonicModule, NavController } from '@ionic/angular';
import { of } from 'rxjs';

import { AuthPage } from './auth.page';
import { AuthService } from './auth.service';

describe('AuthPage', () => {
  let component: AuthPage;
  let fixture: ComponentFixture<AuthPage>;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(waitForAsync(() => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['login', 'register', 'resendVerification']);
    authService.login.and.returnValue(of({
      userId: 1,
      displayName: 'Ava',
      email: 'ava@example.com',
      token: 'token'
    }));
    authService.register.and.returnValue(of({ message: 'Check your inbox.', emailSent: true }));
    authService.resendVerification.and.returnValue(of({ message: 'Email sent.', emailSent: true }));
    TestBed.configureTestingModule({
      declarations: [ AuthPage ],
      imports: [ReactiveFormsModule, IonicModule.forRoot()],
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: NavController, useValue: { navigateRoot: jasmine.createSpy('navigateRoot') } }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(AuthPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('requires matching password confirmation before registration', () => {
    component.toggleMode();
    component.authForm.controls.displayName.setValue('Ava');
    component.authForm.controls.email.setValue('ava@example.com');
    component.authForm.controls.password.setValue('Password123!');
    component.authForm.controls.confirmPassword.setValue('Different123!');

    component.onSubmit();

    expect(component.authError).toBe('Passwords do not match.');
    expect(component.isSubmitting).toBeFalse();
  });

  it('shows a delivery warning when registration succeeds but verification email fails', () => {
    authService.register.and.returnValue(of({
      message: "Account created, but we couldn't send the verification email. Please try resending later.",
      emailSent: false
    }));
    component.toggleMode();
    component.authForm.controls.displayName.setValue('Ava');
    component.authForm.controls.email.setValue('ava@example.com');
    component.authForm.controls.password.setValue('Password123!');
    component.authForm.controls.confirmPassword.setValue('Password123!');

    component.onSubmit();

    expect(component.registrationComplete).toBeTrue();
    expect(component.registrationEmailSent).toBeFalse();
    expect(component.registrationMessage).toContain("couldn't send");
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).not.toContain('token');
  });

  it('does not show resend as successful when delivery fails', () => {
    authService.resendVerification.and.returnValue(of({
      message: "We couldn't send the verification email. Please try again later.",
      emailSent: false
    }));
    component.authForm.controls.email.setValue('ava@example.com');

    component.onResendVerification();

    expect(component.verificationResent).toBeFalse();
    expect(component.verificationError).toContain("couldn't send");
  });
});
