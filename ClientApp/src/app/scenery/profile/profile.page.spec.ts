import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

import { ProfilePage } from './profile.page';
import { UserService } from '../../auth/user.service';
import { AuthService } from '../../auth/auth.service';
import { User } from '../../auth/user.model';

describe('ProfilePage', () => {
  let component: ProfilePage;
  let fixture: ComponentFixture<ProfilePage>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ ProfilePage ],
      providers: [
        {
          provide: UserService,
          useValue: { getCurrentUser: () => of(new User(1, 'Test user', 'test@example.com')) }
        },
        { provide: AuthService, useValue: { currentUserId: 1 } },
        { provide: ActivatedRoute, useValue: { paramMap: of(new Map()) } }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ProfilePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.user?.displayName).toBe('Test user');
  });
});
