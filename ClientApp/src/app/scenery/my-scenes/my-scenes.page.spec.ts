import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { of } from 'rxjs';

import { MyScenesPage } from './my-scenes.page';
import { SceneryService } from '../scenery.service';
import { Scene } from '../scene.model';

describe('MyScenesPage', () => {
  let component: MyScenesPage;
  let fixture: ComponentFixture<MyScenesPage>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ MyScenesPage ],
      providers: [{
        provide: SceneryService,
        useValue: { getScenery: () => of([new Scene(1, 'My scene', 'Description', 'https://example.com/image.jpg', 8)]) }
      }],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(MyScenesPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.myScenes.length).toBe(1);
  });
});
