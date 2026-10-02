import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { AlertController, NavController } from '@ionic/angular';
import { of, throwError } from 'rxjs';

import { SceneDetailPage } from './scene-detail.page';
import { SceneryService } from '../../scenery.service';
import { Scene } from '../../scene.model';
import { AuthService } from '../../../auth/auth.service';
import { User } from '../../../auth/user.model';
import { UserService } from '../../../auth/user.service';
import { Visit } from '../../../visits/visit.model';
import { VisitsService } from '../../../visits/visits.service';
import { ImageUrlPipe } from '../../../shared/image-url.pipe';
import { ContentReportService } from '../../../shared/content-report.service';

describe('SceneDetailPage', () => {
  let component: SceneDetailPage;
  let fixture: ComponentFixture<SceneDetailPage>;
  let sceneryService: jasmine.SpyObj<SceneryService>;
  let visitsService: jasmine.SpyObj<VisitsService>;
  let alertController: jasmine.SpyObj<AlertController>;
  let contentReportService: jasmine.SpyObj<ContentReportService>;

  function configure(scene: Scene, currentUserId: number | null, existingVisits: Visit[] = []) {
    TestBed.resetTestingModule();
    sceneryService = jasmine.createSpyObj('SceneryService', ['getScene', 'rateScene', 'removeRating']);
    sceneryService.getScene.and.returnValue(of(scene));
    sceneryService.rateScene.and.returnValue(of(scene));
    sceneryService.removeRating.and.returnValue(of(undefined));
    visitsService = jasmine.createSpyObj('VisitsService', ['getUserVisits', 'recordVisit', 'removeVisit']);
    visitsService.getUserVisits.and.returnValue(of(existingVisits));
    visitsService.recordVisit.and.returnValue(of({ id: 12, sceneId: scene.id, userId: currentUserId ?? 999, visitedAt: '' }));
    visitsService.removeVisit.and.returnValue(of(undefined));
    alertController = jasmine.createSpyObj('AlertController', ['create']);
    contentReportService = jasmine.createSpyObj('ContentReportService', ['reportProfile', 'reportScene']);
    contentReportService.reportScene.and.returnValue(of({ id: 1, targetType: 'Scene', targetId: scene.id, createdAt: '' }));

    TestBed.configureTestingModule({
      declarations: [ SceneDetailPage ],
      imports: [HttpClientTestingModule, RouterTestingModule, ImageUrlPipe],
      providers: [
        { provide: ActivatedRoute, useValue: { paramMap: of(new Map([['sceneId', '1']])) } },
        { provide: SceneryService, useValue: sceneryService },
        { provide: AuthService, useValue: { currentUserId } },
        { provide: UserService, useValue: { getUser: jasmine.createSpy('getUser').and.returnValue(of(new User(2, 'Scene owner', null))) } },
        { provide: VisitsService, useValue: visitsService },
        { provide: AlertController, useValue: alertController },
        { provide: ContentReportService, useValue: contentReportService },
        { provide: NavController, useValue: { navigateBack: jasmine.createSpy('navigateBack') } }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    });
  }

  beforeEach(waitForAsync(() => {
    configure(new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8), 999);
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(SceneDetailPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.scene?.id).toBe(1);
  });

  it('records a visit and confirms it without leaving the scene', () => {
    const navController = TestBed.inject(NavController);

    component.onVisitScene();

    expect(visitsService.recordVisit).toHaveBeenCalledWith(1);
    expect(component.visitRecorded).toBeTrue();
    expect(component.isVisiting).toBeFalse();
    expect(component.visitButtonLabel).toBe('Unvisit');
    expect(navController.navigateBack).not.toHaveBeenCalled();
  });

  it('loads an existing visit and allows the user to unvisit the scene', () => {
    configure(new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8), 999, [
      { id: 23, sceneId: 1, userId: 999, visitedAt: '2026-10-01T00:00:00Z' }
    ]);
    fixture = TestBed.createComponent(SceneDetailPage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.visitRecorded).toBeTrue();
    expect(component.visitButtonLabel).toBe('Unvisit');
    component.onVisitScene();

    expect(visitsService.removeVisit).toHaveBeenCalledWith(23);
    expect(component.visitRecorded).toBeFalse();
    expect(component.visitButtonLabel).toBe('Mark as visited');
  });

  it('is not the owner when the current user does not own the scene', () => {
    expect(component.isOwnScene).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Report scene');
    expect(fixture.nativeElement.textContent).toContain('Mark as visited');
  });

  it('prompts for a description and submits a scene report', async () => {
    alertController.create.and.resolveTo({
      present: jasmine.createSpy('present').and.resolveTo(undefined),
      onDidDismiss: jasmine.createSpy('onDidDismiss').and.resolveTo({
        role: 'confirm',
        data: { values: { description: 'This scene image is inappropriate.' } }
      })
    } as any);

    await component.reportScene();

    expect(contentReportService.reportScene).toHaveBeenCalledWith(1, {
      description: 'This scene image is inappropriate.'
    });
    expect(component.reportFeedback).toContain('Report submitted');
  });

  it('links the scene owner to their public profile', () => {
    configure(new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8,
      45, -93, [], undefined, undefined, 0, undefined, 2), 999);
    fixture = TestBed.createComponent(SceneDetailPage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const ownerLink = fixture.nativeElement.querySelector('.scene-owner');
    expect(ownerLink?.textContent).toContain('Scene owner');
    expect(ownerLink?.getAttribute('href')).toBe('/scenery/tabs/profile/2');
  });

  it('is the owner when the current user owns the scene', () => {
    configure(new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8,
      undefined, undefined, [], undefined, undefined, 0, undefined, 999), 999);
    fixture = TestBed.createComponent(SceneDetailPage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.isOwnScene).toBe(true);
  });

  it('submits a rating and updates the scene', () => {
    component.pendingRating = 7;
    component.ratingDescription = 'A lovely view.';

    component.onSubmitRating();

    expect(sceneryService.rateScene).toHaveBeenCalledWith(1, 7, 'A lovely view.');
    expect(component.isRating).toBe(false);
    expect(component.ratingError).toBe(false);
  });

  it('does not submit a rating when none is entered', () => {
    component.pendingRating = null;

    component.onSubmitRating();

    expect(sceneryService.rateScene).not.toHaveBeenCalled();
  });

  it('does not submit a rating outside the allowed range', () => {
    component.pendingRating = 11;

    component.onSubmitRating();

    expect(sceneryService.rateScene).not.toHaveBeenCalled();
  });

  it('sets ratingError when submitting a rating fails', () => {
    sceneryService.rateScene.and.returnValue(throwError(() => new Error('failed')));
    component.pendingRating = 7;

    component.onSubmitRating();

    expect(component.ratingError).toBe(true);
    expect(component.isRating).toBe(false);
  });

  it('removes an existing rating', () => {
    configure(new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8,
      undefined, undefined, [], undefined, undefined, 1, 7, undefined, undefined, undefined, true,
      'Previously shared feedback.'), 999);
    fixture = TestBed.createComponent(SceneDetailPage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    component.onRemoveRating();

    expect(sceneryService.removeRating).toHaveBeenCalledWith(1);
    expect(component.scene?.currentUserRating).toBeUndefined();
    expect(component.scene?.currentUserRatingDescription).toBeUndefined();
    expect(component.pendingRating).toBeNull();
    expect(component.ratingDescription).toBe('');
  });

  it('loads and displays individual ratings with written feedback', () => {
    const sceneWithRatings = new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8,
      undefined, undefined, [], undefined, 8, 1, 8, 2, undefined, undefined, true,
      'My feedback.', [{ userDisplayName: 'Rater One', rating: 8, description: 'Wonderful viewpoint.', createdAt: '2026-10-01T00:00:00Z' }]);
    configure(sceneWithRatings, 999);
    fixture = TestBed.createComponent(SceneDetailPage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.ratingDescription).toBe('My feedback.');
    expect(fixture.nativeElement.textContent).toContain('Individual ratings');
    expect(fixture.nativeElement.textContent).toContain('Rater One');
    expect(fixture.nativeElement.textContent).toContain('Wonderful viewpoint.');
    expect(fixture.nativeElement.textContent).toContain('8 / 10');
  });

  it('does not attempt to remove a rating when none exists', () => {
    component.onRemoveRating();

    expect(sceneryService.removeRating).not.toHaveBeenCalled();
  });
});
