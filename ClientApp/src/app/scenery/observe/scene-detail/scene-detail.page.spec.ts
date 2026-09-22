import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { NavController } from '@ionic/angular';
import { of } from 'rxjs';

import { SceneDetailPage } from './scene-detail.page';
import { SceneryService } from '../../scenery.service';
import { Scene } from '../../scene.model';
import { VisitsService } from '../../../visits/visits.service';

describe('SceneDetailPage', () => {
  let component: SceneDetailPage;
  let fixture: ComponentFixture<SceneDetailPage>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ SceneDetailPage ],
      imports: [HttpClientTestingModule],
      providers: [
        { provide: ActivatedRoute, useValue: { paramMap: of(new Map([['sceneId', '1']])) } },
        { provide: SceneryService, useValue: { getScene: () => of(new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8)) } },
        { provide: VisitsService, useValue: { recordVisit: jasmine.createSpy('recordVisit').and.returnValue(of({})) } },
        { provide: NavController, useValue: { navigateBack: jasmine.createSpy('navigateBack') } }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
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
});
