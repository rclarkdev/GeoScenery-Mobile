import { Component, OnInit } from '@angular/core';
import { AlertController } from '@ionic/angular';
import { forkJoin } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { AdminContentReport, AdminReportStatus, AdminService, AdminUser } from './admin.service';

@Component({
  selector: 'app-admin',
  templateUrl: './admin.page.html',
  styleUrls: ['./admin.page.scss']
})
export class AdminPage implements OnInit {
  section: 'reports' | 'users' = 'reports';
  reportFilter: AdminReportStatus | 'All' = 'Pending';
  userSearch = '';
  reports: AdminContentReport[] = [];
  users: AdminUser[] = [];
  isLoading = true;
  busyKey: string | null = null;
  errorMessage: string | null = null;
  notice: string | null = null;

  constructor(private adminService: AdminService, private alerts: AlertController, private authService: AuthService) { }

  ngOnInit(): void {
    this.reload();
  }

  get filteredUsers(): AdminUser[] {
    const term = this.userSearch.trim().toLocaleLowerCase();
    return term
      ? this.users.filter(user => `${user.displayName} ${user.email}`.toLocaleLowerCase().includes(term))
      : this.users;
  }

  reload(): void {
    this.isLoading = true;
    this.errorMessage = null;
    forkJoin({ users: this.adminService.getUsers(), reports: this.adminService.getReports(this.reportFilter) }).subscribe({
      next: result => {
        this.users = result.users;
        this.reports = result.reports;
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.errorMessage = 'Admin data could not be loaded. Check your access and connection, then retry.';
      }
    });
  }

  setSection(section: 'reports' | 'users'): void {
    this.section = section;
    this.notice = null;
  }

  setReportFilter(filter: AdminReportStatus | 'All'): void {
    this.reportFilter = filter;
    this.adminService.getReports(filter).subscribe({
      next: reports => this.reports = reports,
      error: () => this.errorMessage = 'Reports could not be loaded.'
    });
  }

  isAdmin(user: AdminUser): boolean {
    return user.roles.includes('Admin');
  }

  isCurrentUser(user: AdminUser): boolean {
    return user.id === this.authService.currentUserId;
  }

  async toggleAdminRole(user: AdminUser): Promise<void> {
    const adding = !this.isAdmin(user);
    const roles = adding ? [...user.roles, 'Admin'] : user.roles.filter(role => role !== 'Admin');
    const alert = await this.alerts.create({
      header: adding ? `Grant Admin to ${user.displayName}?` : `Remove Admin from ${user.displayName}?`,
      message: adding
        ? 'This account will be able to manage administrator controls.'
        : 'This account will lose administrator access immediately. The last administrator cannot be demoted.',
      buttons: [
        { text: 'Cancel', role: 'cancel' },
        { text: adding ? 'Grant access' : 'Remove access', role: 'confirm' }
      ]
    });
    await alert.present();
    if ((await alert.onDidDismiss()).role !== 'confirm') {
      return;
    }

    this.busyKey = `role-${user.id}`;
    this.clearFeedback();
    this.adminService.updateUserRoles(user.id, roles).subscribe({
      next: updated => {
        this.users = this.users.map(candidate => candidate.id === updated.id ? updated : candidate);
        this.busyKey = null;
        this.notice = adding ? 'Administrator role granted.' : 'Administrator role removed.';
      },
      error: () => {
        this.busyKey = null;
        this.errorMessage = 'Role change was rejected. The last administrator must remain assigned.';
      }
    });
  }

  async deleteUser(user: AdminUser): Promise<void> {
    const alert = await this.alerts.create({
      header: `Delete ${user.displayName}'s account?`,
      message: 'This permanently deletes the account and its follows, blocks, messages, visits, and ratings. Their scenes remain but are no longer linked to the account. This cannot be undone.',
      buttons: [
        { text: 'Cancel', role: 'cancel' },
        { text: 'Delete account', role: 'confirm' }
      ]
    });
    await alert.present();
    if ((await alert.onDidDismiss()).role !== 'confirm') {
      return;
    }

    this.busyKey = `delete-user-${user.id}`;
    this.clearFeedback();
    this.adminService.deleteUser(user.id).subscribe({
      next: () => {
        this.users = this.users.filter(candidate => candidate.id !== user.id);
        this.busyKey = null;
        this.notice = `Deleted ${user.displayName}'s account.`;
        this.reloadReports();
      },
      error: () => {
        this.busyKey = null;
        this.errorMessage = 'Account deletion was rejected. An administrator cannot delete their own or the last administrator account.';
      }
    });
  }

  updateStatus(report: AdminContentReport, status: AdminReportStatus): void {
    this.busyKey = `report-${report.id}`;
    this.clearFeedback();
    const notes = status === 'Dismissed' ? 'Reviewed by an administrator; no action taken.'
      : status === 'Reviewed' ? 'Reviewed by an administrator.' : undefined;
    this.adminService.updateReport(report.id, status, notes).subscribe({
      next: updated => {
        this.reports = this.reports.map(candidate => candidate.id === updated.id ? updated : candidate);
        this.busyKey = null;
        this.notice = `Report marked ${status.toLocaleLowerCase()}.`;
      },
      error: () => {
        this.busyKey = null;
        this.errorMessage = 'Unable to update this report.';
      }
    });
  }

  async removeReportedContent(report: AdminContentReport): Promise<void> {
    const contentName = report.targetType === 'Scene' ? 'scene' : 'profile';
    const alert = await this.alerts.create({
      header: `Remove reported ${contentName}?`,
      message: `This will permanently delete the ${contentName} “${report.targetLabel}”. Related reports will be marked as actioned.`,
      buttons: [
        { text: 'Cancel', role: 'cancel' },
        { text: 'Remove content', role: 'confirm' }
      ]
    });
    await alert.present();
    if ((await alert.onDidDismiss()).role !== 'confirm') {
      return;
    }

    this.busyKey = `remove-content-${report.id}`;
    this.clearFeedback();
    const removal = report.targetType === 'Scene'
      ? this.adminService.deleteScene(report.targetId)
      : this.adminService.deleteUser(report.targetId);
    removal.subscribe({
      next: () => {
        if (report.targetType === 'Profile') {
          this.users = this.users.filter(user => user.id !== report.targetId);
        }
        this.busyKey = null;
        this.notice = `Reported ${contentName} removed.`;
        this.reloadReports();
      },
      error: () => {
        this.busyKey = null;
        this.errorMessage = 'Unable to remove the reported content. It may already be removed or protected as the last administrator account.';
      }
    });
  }

  statusColor(status: AdminReportStatus): string {
    return status === 'Pending' ? 'warning'
      : status === 'Actioned' ? 'success'
        : status === 'Dismissed' ? 'medium' : 'primary';
  }

  private reloadReports(): void {
    this.adminService.getReports(this.reportFilter).subscribe({
      next: reports => this.reports = reports,
      error: () => this.errorMessage = 'Reports could not be refreshed.'
    });
  }

  private clearFeedback(): void {
    this.errorMessage = null;
    this.notice = null;
  }
}
