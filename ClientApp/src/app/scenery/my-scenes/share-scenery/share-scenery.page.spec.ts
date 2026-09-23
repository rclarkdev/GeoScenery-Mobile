import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';

import { ShareSceneryPage } from './share-scenery.page';
import { SceneryService } from '../../scenery.service';
import { Scene } from '../../scene.model';
import { AuthService } from '../../../auth/auth.service';

describe('ShareSceneryPage', () => {
  let component: ShareSceneryPage;
  let fixture: ComponentFixture<ShareSceneryPage>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ ShareSceneryPage ],
      providers: [
        { provide: ActivatedRoute, useValue: { paramMap: of(new Map([['sceneId', '1']])) } },
        { provide: SceneryService, useValue: { getScene: () => of(new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8)) } }
        ,{ provide: AuthService, useValue: { currentUserId: 1 } }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ShareSceneryPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.scene?.id).toBe(1);
  });
});
