import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { NavController } from '@ionic/angular';
import { of, throwError } from 'rxjs';

import { SceneDetailPage } from './scene-detail.page';
import { SceneryService } from '../../scenery.service';
import { Scene } from '../../scene.model';
import { AuthService } from '../../../auth/auth.service';
import { VisitsService } from '../../../visits/visits.service';

describe('SceneDetailPage', () => {
  let component: SceneDetailPage;
  let fixture: ComponentFixture<SceneDetailPage>;
  let sceneryService: jasmine.SpyObj<SceneryService>;

  function configure(scene: Scene, currentUserId: number | null) {
    TestBed.resetTestingModule();
    sceneryService = jasmine.createSpyObj('SceneryService', ['getScene', 'rateScene', 'removeRating']);
    sceneryService.getScene.and.returnValue(of(scene));
    sceneryService.rateScene.and.returnValue(of(scene));
    sceneryService.removeRating.and.returnValue(of(undefined));

    TestBed.configureTestingModule({
      declarations: [ SceneDetailPage ],
      imports: [HttpClientTestingModule],
      providers: [
        { provide: ActivatedRoute, useValue: { paramMap: of(new Map([['sceneId', '1']])) } },
        { provide: SceneryService, useValue: sceneryService },
        { provide: AuthService, useValue: { currentUserId } },
        { provide: VisitsService, useValue: { recordVisit: jasmine.createSpy('recordVisit').and.returnValue(of({})) } },
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

  it('should record a visit and navigate to visit history', () => {
    const visitsService = TestBed.inject(VisitsService);
    const navController = TestBed.inject(NavController);

    component.onVisitScene();

    expect(visitsService.recordVisit).toHaveBeenCalledWith(1);
    expect(navController.navigateBack).toHaveBeenCalledWith('/visits');
  });

  it('is not the owner when the current user does not own the scene', () => {
    expect(component.isOwnScene).toBe(false);
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

    component.onSubmitRating();

    expect(sceneryService.rateScene).toHaveBeenCalledWith(1, 7);
    expect(component.isRating).toBe(false);
    expect(component.ratingError).toBe(false);
  });

  it('does not submit a rating when none is entered', () => {
    component.pendingRating = null;

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
      undefined, undefined, [], undefined, undefined, 1, 7), 999);
    fixture = TestBed.createComponent(SceneDetailPage);
    component = fixture.componentInstance;
    fixture.detectChanges();

    component.onRemoveRating();

    expect(sceneryService.removeRating).toHaveBeenCalledWith(1);
    expect(component.scene?.currentUserRating).toBeUndefined();
    expect(component.pendingRating).toBeNull();
  });

  it('does not attempt to remove a rating when none exists', () => {
    component.onRemoveRating();

    expect(sceneryService.removeRating).not.toHaveBeenCalled();
  });
});
