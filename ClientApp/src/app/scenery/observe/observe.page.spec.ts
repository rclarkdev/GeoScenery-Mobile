import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular';
import { of } from 'rxjs';

import { ObservePage } from './observe.page';
import { SceneryService } from '../scenery.service';
import { Scene } from '../scene.model';

describe('ObservePage', () => {
  let component: ObservePage;
  let fixture: ComponentFixture<ObservePage>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ ObservePage ],
      imports: [ReactiveFormsModule, IonicModule.forRoot()],
      providers: [{
        provide: SceneryService,
        useValue: { getScenery: () => of([new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8)]) }
      }],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(ObservePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.loadedScenery.length).toBe(1);
  });
});
