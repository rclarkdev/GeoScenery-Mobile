import { CommonModule } from '@angular/common';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormsModule } from '@angular/forms';
import { AlertController, IonicModule } from '@ionic/angular';
import { of, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { AdminContentReport, AdminService, AdminUser } from './admin.service';
import { AdminPage } from './admin.page';

describe('AdminPage', () => {
  let fixture: ComponentFixture<AdminPage>;
  let component: AdminPage;
  let service: jasmine.SpyObj<AdminService>;
  let alerts: jasmine.SpyObj<AlertController>;

  const user: AdminUser = {
    id: 9,
    displayName: 'Sample Member',
    email: 'member@example.com',
    roles: ['Member'],
    isSuspended: false,
    suspendedAt: null,
    suspensionReason: null
  };
  const report: AdminContentReport = {
    id: 3,
    targetType: 'Scene',
    targetId: 22,
    targetLabel: 'Creek crossing',
    reporterDisplayName: 'Reporter',
    reporterEmail: 'reporter@example.com',
    description: 'Please review this scene.',
    status: 'Pending',
    createdAt: '2026-10-01T12:00:00Z',
    targetExists: true,
    targetIsHidden: false,
    targetIsSuspended: false
  };

  function page(items: any[], totalCount = items.length) {
    return { items, page: 1, pageSize: 25, totalCount };
  }

  beforeEach(() => {
    service = jasmine.createSpyObj<AdminService>('AdminService', [
      'getUsers', 'updateUserRoles', 'deleteUser', 'getReports', 'updateReport', 'deleteScene',
      'updateUserSuspension', 'setSceneVisibility', 'getAudit', 'getScenes'
    ]);
    service.getUsers.and.returnValue(of(page([user])));
    service.getReports.and.returnValue(of(page([report])));
    service.getAudit.and.returnValue(of(page([])));
    service.getScenes.and.returnValue(of(page([])));
    service.updateUserRoles.and.returnValue(of({ ...user, roles: ['Member', 'Admin'] }));
    service.deleteUser.and.returnValue(of(undefined));
    service.deleteScene.and.returnValue(of(undefined));
    service.updateUserSuspension.and.returnValue(of({ ...user, isSuspended: true }));
    service.setSceneVisibility.and.returnValue(of(undefined));
    service.updateReport.and.returnValue(of({ ...report, status: 'Reviewed' }));

    alerts = jasmine.createSpyObj<AlertController>('AlertController', ['create']);
    TestBed.configureTestingModule({
      imports: [CommonModule, FormsModule, IonicModule.forRoot()],
      declarations: [AdminPage],
      providers: [
        { provide: AdminService, useValue: service },
        { provide: AlertController, useValue: alerts },
        { provide: AuthService, useValue: { currentUserId: 1 } }
      ],
      schemas: [NO_ERRORS_SCHEMA]
    }).compileComponents();

    fixture = TestBed.createComponent(AdminPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads and displays users and reports', () => {
    expect(service.getUsers).toHaveBeenCalledWith(1, 25, '');
    expect(service.getReports).toHaveBeenCalledWith('Pending', 1, 25, 'All', '');
    expect(component.users[0].displayName).toBe('Sample Member');
    expect(fixture.nativeElement.textContent).toContain('Creek crossing');
  });

  it('sends user search and page changes to the server', () => {
    component.setUserSearch('ava');
    component.changePage('users', 1);

    expect(service.getUsers).toHaveBeenCalledWith(1, 25, 'ava');
    expect(service.getUsers).toHaveBeenCalledWith(2, 25, 'ava');
  });

  it('sends report status, target, and search filters to the server', () => {
    component.setReportFilter('Reviewed');
    component.setReportTargetFilter('Profile');
    component.setReportSearch('abuse');

    expect(service.getReports).toHaveBeenCalledWith('Reviewed', 1, 25, 'All', '');
    expect(service.getReports).toHaveBeenCalledWith('Reviewed', 1, 25, 'Profile', '');
    expect(service.getReports).toHaveBeenCalledWith('Reviewed', 1, 25, 'Profile', 'abuse');
  });

  it('displays a user-visible load error', () => {
    service.getReports.and.returnValue(throwError(() => new Error('offline')));
    component.reload();
    fixture.detectChanges();

    expect(component.errorMessage).toContain('Admin data could not be loaded');
    expect(fixture.nativeElement.textContent).toContain('Admin data could not be loaded');
  });

  it('does not update a role when the confirmation is cancelled', async () => {
    alerts.create.and.resolveTo({
      present: jasmine.createSpy('present').and.resolveTo(undefined),
      onDidDismiss: jasmine.createSpy('onDidDismiss').and.resolveTo({ role: 'cancel' })
    } as any);

    await component.toggleAdminRole(user);

    expect(service.updateUserRoles).not.toHaveBeenCalled();
  });

  it('updates role state after confirmed role assignment', async () => {
    alerts.create.and.resolveTo({
      present: jasmine.createSpy('present').and.resolveTo(undefined),
      onDidDismiss: jasmine.createSpy('onDidDismiss').and.resolveTo({ role: 'confirm' })
    } as any);

    await component.toggleAdminRole(user);

    expect(service.updateUserRoles).toHaveBeenCalledWith(user.id, ['Member', 'Admin']);
    expect(component.users[0].roles).toContain('Admin');
    expect(component.notice).toBe('Administrator role granted.');
  });

  it('shows an error when a role update is rejected', async () => {
    service.updateUserRoles.and.returnValue(throwError(() => new Error('last admin')));
    alerts.create.and.resolveTo({
      present: jasmine.createSpy('present').and.resolveTo(undefined),
      onDidDismiss: jasmine.createSpy('onDidDismiss').and.resolveTo({ role: 'confirm' })
    } as any);

    await component.toggleAdminRole({ ...user, roles: ['Admin'] });

    expect(component.errorMessage).toContain('last administrator');
  });
});
