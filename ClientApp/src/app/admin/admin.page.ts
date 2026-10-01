import { Component, OnInit } from '@angular/core';
import { AlertController } from '@ionic/angular';
import { forkJoin, map } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { AdminActionAudit, AdminContentReport, AdminReportStatus, AdminReportTarget, AdminScene, AdminService, AdminUser, PagedResult } from './admin.service';

@Component({
  selector: 'app-admin',
  templateUrl: './admin.page.html',
  styleUrls: ['./admin.page.scss']
})
export class AdminPage implements OnInit {
  section: 'reports' | 'users' | 'scenes' | 'audit' = 'reports';
  reportFilter: AdminReportStatus | 'All' = 'Pending';
  reportTargetFilter: AdminReportTarget | 'All' = 'All';
  reportSearch = '';
  reportPage = 1;
  readonly pageSize = 25;
  reportTotalCount = 0;
  userSearch = '';
  userPage = 1;
  userTotalCount = 0;
  auditPage = 1;
  auditTotalCount = 0;
  scenePage = 1;
  sceneTotalCount = 0;
  sceneSearch = '';
  sceneVisibilityFilter: 'All' | 'Visible' | 'Hidden' = 'All';
  reports: AdminContentReport[] = [];
  users: AdminUser[] = [];
  audits: AdminActionAudit[] = [];
  scenes: AdminScene[] = [];
  isLoading = true;
  busyKey: string | null = null;
  errorMessage: string | null = null;
  notice: string | null = null;

  constructor(private adminService: AdminService, private alerts: AlertController, private authService: AuthService) { }

  ngOnInit(): void {
    this.reload();
  }

  reload(): void {
    this.isLoading = true;
    this.errorMessage = null;
    forkJoin({
      users: this.adminService.getUsers(this.userPage, this.pageSize, this.userSearch),
      reports: this.adminService.getReports(this.reportFilter, this.reportPage, this.pageSize, this.reportTargetFilter, this.reportSearch),
      scenes: this.adminService.getScenes(this.scenePage, this.pageSize, this.sceneSearch, this.sceneVisibilityFilter === 'All' ? undefined : this.sceneVisibilityFilter === 'Hidden'),
      audits: this.adminService.getAudit(this.auditPage, this.pageSize)
    }).subscribe({
      next: result => {
        this.applyPage(result.users, 'users');
        this.applyPage(result.reports, 'reports');
        this.applyPage(result.scenes, 'scenes');
        this.applyPage(result.audits, 'audits');
        this.isLoading = false;
      },
      error: () => {
        this.isLoading = false;
        this.errorMessage = 'Admin data could not be loaded. Check your access and connection, then retry.';
      }
    });
  }

  setSection(section: 'reports' | 'users' | 'scenes' | 'audit'): void {
    this.section = section;
    this.notice = null;
  }

  setReportFilter(filter: AdminReportStatus | 'All'): void {
    this.reportFilter = filter;
    this.reportPage = 1;
    this.loadReports();
  }

  setReportTargetFilter(filter: AdminReportTarget | 'All'): void {
    this.reportTargetFilter = filter;
    this.reportPage = 1;
    this.loadReports();
  }

  setReportSearch(search: string): void {
    this.reportSearch = search;
    this.reportPage = 1;
    this.loadReports();
  }

  setUserSearch(search: string): void {
    this.userSearch = search;
    this.userPage = 1;
    this.loadUsers();
  }

  setSceneSearch(search: string): void {
    this.sceneSearch = search;
    this.scenePage = 1;
    this.loadScenes();
  }

  setSceneVisibilityFilter(filter: 'All' | 'Visible' | 'Hidden'): void {
    this.sceneVisibilityFilter = filter;
    this.scenePage = 1;
    this.loadScenes();
  }

  changePage(kind: 'reports' | 'users' | 'scenes' | 'audit', delta: number): void {
    if (kind === 'reports') {
      this.reportPage = Math.max(1, this.reportPage + delta);
      this.loadReports();
    } else if (kind === 'users') {
      this.userPage = Math.max(1, this.userPage + delta);
      this.loadUsers();
    } else if (kind === 'scenes') {
      this.scenePage = Math.max(1, this.scenePage + delta);
      this.loadScenes();
    } else {
      this.auditPage = Math.max(1, this.auditPage + delta);
      this.loadAudits();
    }
  }

  canGoNext(kind: 'reports' | 'users' | 'scenes' | 'audit'): boolean {
    const page = kind === 'reports' ? this.reportPage : kind === 'users' ? this.userPage : kind === 'scenes' ? this.scenePage : this.auditPage;
    const total = kind === 'reports' ? this.reportTotalCount : kind === 'users' ? this.userTotalCount : kind === 'scenes' ? this.sceneTotalCount : this.auditTotalCount;
    return page * this.pageSize < total;
  }

  isAdmin(user: AdminUser): boolean {
    return user.roles.includes('Admin');
  }

  isCurrentUser(user: AdminUser): boolean {
    return user.id === this.authService.currentUserId;
  }

  isOnlyActiveAdmin(user: AdminUser): boolean {
    return this.isAdmin(user) && !user.isSuspended
      && this.users.filter(candidate => this.isAdmin(candidate) && !candidate.isSuspended).length <= 1
      && this.userTotalCount <= this.users.length;
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
        this.busyKey = null;
        this.notice = `Deleted ${user.displayName}'s account.`;
        this.reload();
      },
      error: () => {
        this.busyKey = null;
        this.errorMessage = 'Account deletion was rejected. An administrator cannot delete their own or the last administrator account.';
      }
    });
  }

  async toggleSuspension(user: AdminUser): Promise<void> {
    const isSuspending = !user.isSuspended;
    const alert = await this.alerts.create({
      header: isSuspending ? `Suspend ${user.displayName}?` : `Restore ${user.displayName}?`,
      message: isSuspending ? 'Suspended accounts cannot access authenticated app features. Enter a reason.'
        : 'The account will be able to sign in and use the app again.',
      inputs: isSuspending ? [{ name: 'reason', type: 'textarea', placeholder: 'Reason for suspension', attributes: { maxlength: 1000 } }] : [],
      buttons: [{ text: 'Cancel', role: 'cancel' }, { text: isSuspending ? 'Suspend account' : 'Restore account', role: 'confirm' }]
    });
    await alert.present();
    const result = await alert.onDidDismiss();
    if (result.role !== 'confirm') return;
    const reason = String(result.data?.values?.reason ?? '').trim();
    if (isSuspending && !reason) {
      this.errorMessage = 'Enter a reason before suspending an account.';
      return;
    }

    this.busyKey = `suspension-${user.id}`;
    this.clearFeedback();
    this.adminService.updateUserSuspension(user.id, isSuspending, reason || undefined).subscribe({
      next: updated => {
        this.users = this.users.map(candidate => candidate.id === updated.id ? updated : candidate);
        this.busyKey = null;
        this.notice = isSuspending ? 'Account suspended.' : 'Account restored.';
      },
      error: () => {
        this.busyKey = null;
        this.errorMessage = 'Unable to change account suspension. The last administrator cannot be suspended.';
      }
    });
  }

  async toggleSceneVisibility(scene: AdminScene): Promise<void> {
    const hiding = !scene.isHidden;
    const alert = await this.alerts.create({
      header: hiding ? `Hide ${scene.title}?` : `Restore ${scene.title}?`,
      message: hiding ? 'This scene will disappear from public search and detail views. Enter a reason.'
        : 'This scene will become visible to the community again.',
      inputs: hiding ? [{ name: 'reason', type: 'textarea', placeholder: 'Reason for hiding this scene', attributes: { maxlength: 1000 } }] : [],
      buttons: [{ text: 'Cancel', role: 'cancel' }, { text: hiding ? 'Hide scene' : 'Restore scene', role: 'confirm' }]
    });
    await alert.present();
    const result = await alert.onDidDismiss();
    if (result.role !== 'confirm') return;
    const reason = String(result.data?.values?.reason ?? '').trim();
    if (hiding && !reason) {
      this.errorMessage = 'Enter a reason before hiding a scene.';
      return;
    }

    this.busyKey = `scene-${scene.id}`;
    this.clearFeedback();
    this.adminService.setSceneVisibility(scene.id, hiding, reason || undefined).subscribe({
      next: () => {
        this.busyKey = null;
        this.notice = hiding ? 'Scene hidden from public view.' : 'Scene restored to public view.';
        this.loadScenes();
        this.loadReports();
      },
      error: () => {
        this.busyKey = null;
        this.errorMessage = 'Unable to change scene visibility.';
      }
    });
  }

  async deleteScene(scene: AdminScene): Promise<void> {
    const alert = await this.alerts.create({
      header: `Permanently delete ${scene.title}?`,
      message: 'This cannot be undone. Hiding the scene is the reversible alternative.',
      buttons: [{ text: 'Cancel', role: 'cancel' }, { text: 'Delete permanently', role: 'confirm' }]
    });
    await alert.present();
    if ((await alert.onDidDismiss()).role !== 'confirm') return;

    this.busyKey = `scene-delete-${scene.id}`;
    this.clearFeedback();
    this.adminService.deleteScene(scene.id).subscribe({
      next: () => {
        this.busyKey = null;
        this.notice = 'Scene permanently deleted.';
        this.loadScenes();
        this.loadReports();
      },
      error: () => {
        this.busyKey = null;
        this.errorMessage = 'Unable to delete this scene.';
      }
    });
  }

  async updateStatus(report: AdminContentReport, status: AdminReportStatus): Promise<void> {
    let notes = status === 'Reviewed' ? 'Reviewed by an administrator.' : '';
    if (status === 'Dismissed' || status === 'Actioned') {
      const alert = await this.alerts.create({
        header: status === 'Dismissed' ? 'Dismiss this report?' : 'Mark this report actioned?',
        message: status === 'Dismissed'
          ? 'Explain why this report does not require moderation action.'
          : 'Describe the moderation action taken.',
        inputs: [{ name: 'notes', type: 'textarea', placeholder: 'Resolution notes', attributes: { maxlength: 1000 } }],
        buttons: [
          { text: 'Cancel', role: 'cancel' },
          { text: status === 'Dismissed' ? 'Dismiss report' : 'Mark actioned', role: 'confirm' }
        ]
      });
      await alert.present();
      const result = await alert.onDidDismiss();
      if (result.role !== 'confirm') return;
      notes = String(result.data?.values?.notes ?? '').trim();
      if (!notes) {
        this.errorMessage = 'Resolution notes are required.';
        return;
      }
    }

    this.busyKey = `report-${report.id}`;
    this.clearFeedback();
    this.adminService.updateReport(report.id, status, notes, status === 'Actioned' ? notes : undefined).subscribe({
      next: updated => {
        this.reports = this.reports.map(candidate => candidate.id === updated.id ? updated : candidate);
        this.busyKey = null;
        this.notice = `Report marked ${status.toLocaleLowerCase()}.`;
        this.loadReports();
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

  async toggleReportedVisibility(report: AdminContentReport): Promise<void> {
    const hiding = report.targetType === 'Scene' ? !report.targetIsHidden : !report.targetIsSuspended;
    const targetName = report.targetType === 'Scene' ? 'scene' : 'account';
    const alert = await this.alerts.create({
      header: hiding ? `Hide reported ${targetName}?` : `Restore reported ${targetName}?`,
      message: hiding ? 'This temporarily removes the content from public access. Enter a reason.'
        : 'This makes the content visible or active again.',
      inputs: hiding ? [{ name: 'reason', type: 'textarea', placeholder: 'Reason for moderation', attributes: { maxlength: 1000 } }] : [],
      buttons: [{ text: 'Cancel', role: 'cancel' }, { text: hiding ? 'Hide content' : 'Restore content', role: 'confirm' }]
    });
    await alert.present();
    const result = await alert.onDidDismiss();
    if (result.role !== 'confirm') return;
    const reason = String(result.data?.values?.reason ?? '').trim();
    if (hiding && !reason) {
      this.errorMessage = 'Enter a reason before applying this moderation action.';
      return;
    }

    this.busyKey = `visibility-${report.id}`;
    this.clearFeedback();
    const operation = report.targetType === 'Scene'
      ? this.adminService.setSceneVisibility(report.targetId, hiding, reason || undefined)
      : this.adminService.updateUserSuspension(report.targetId, hiding, reason || undefined).pipe(map(() => undefined));
    operation.subscribe({
      next: () => {
        this.busyKey = null;
        this.notice = hiding ? `Reported ${targetName} hidden.` : `Reported ${targetName} restored.`;
        this.loadReports();
        if (report.targetType === 'Profile') this.loadUsers();
      },
      error: () => {
        this.busyKey = null;
        this.errorMessage = `Unable to ${hiding ? 'hide' : 'restore'} the reported ${targetName}.`;
      }
    });
  }

  statusColor(status: AdminReportStatus): string {
    return status === 'Pending' ? 'warning'
      : status === 'Actioned' ? 'success'
        : status === 'Dismissed' ? 'medium' : 'primary';
  }

  private reloadReports(): void {
    this.loadReports();
  }

  private loadReports(): void {
    this.adminService.getReports(this.reportFilter, this.reportPage, this.pageSize, this.reportTargetFilter, this.reportSearch).subscribe({
      next: result => this.applyPage(result, 'reports'),
      error: () => this.errorMessage = 'Reports could not be refreshed.'
    });
  }

  private loadUsers(): void {
    this.adminService.getUsers(this.userPage, this.pageSize, this.userSearch).subscribe({
      next: result => this.applyPage(result, 'users'),
      error: () => this.errorMessage = 'Users could not be refreshed.'
    });
  }

  private loadAudits(): void {
    this.adminService.getAudit(this.auditPage, this.pageSize).subscribe({
      next: result => this.applyPage(result, 'audits'),
      error: () => this.errorMessage = 'Admin audit history could not be refreshed.'
    });
  }

  private loadScenes(): void {
    const hidden = this.sceneVisibilityFilter === 'All' ? undefined : this.sceneVisibilityFilter === 'Hidden';
    this.adminService.getScenes(this.scenePage, this.pageSize, this.sceneSearch, hidden).subscribe({
      next: result => this.applyPage(result, 'scenes'),
      error: () => this.errorMessage = 'Scenes could not be refreshed.'
    });
  }

  private applyPage<T>(result: PagedResult<T>, kind: 'users' | 'reports' | 'scenes' | 'audits'): void {
    if (kind === 'users') {
      this.users = result.items as AdminUser[];
      this.userTotalCount = result.totalCount;
    } else if (kind === 'reports') {
      this.reports = result.items as AdminContentReport[];
      this.reportTotalCount = result.totalCount;
    } else if (kind === 'scenes') {
      this.scenes = result.items as AdminScene[];
      this.sceneTotalCount = result.totalCount;
    } else {
      this.audits = result.items as AdminActionAudit[];
      this.auditTotalCount = result.totalCount;
    }
  }

  private clearFeedback(): void {
    this.errorMessage = null;
    this.notice = null;
  }
}
