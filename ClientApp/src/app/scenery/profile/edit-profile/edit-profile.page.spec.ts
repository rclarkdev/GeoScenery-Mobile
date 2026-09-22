import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { IonicModule, NavController } from '@ionic/angular';
import { of } from 'rxjs';

import { EditProfilePage } from './edit-profile.page';
import { AuthService } from '../../../auth/auth.service';
import { UserService } from '../../../auth/user.service';
import { User } from '../../../auth/user.model';

describe('EditProfilePage', () => {
  let component: EditProfilePage;
  let fixture: ComponentFixture<EditProfilePage>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ EditProfilePage ],
      imports: [ReactiveFormsModule, IonicModule.forRoot()],
      providers: [
        { provide: AuthService, useValue: { currentUserId: 1 } },
        {
          provide: UserService,
          useValue: {
            getCurrentUser: () => of(new User(1, 'Test user', 'test@example.com')),
            updateUser: () => of(new User(1, 'Test user', 'test@example.com'))
          }
        },
        { provide: NavController, useValue: { navigateBack: jasmine.createSpy('navigateBack') } }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(EditProfilePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.profileForm.controls.displayName.value).toBe('Test user');
  });
});
