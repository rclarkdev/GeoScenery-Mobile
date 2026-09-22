import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { IonicModule, NavController } from '@ionic/angular';
import { of } from 'rxjs';

import { NewScenePage } from './new-scene.page';
import { SceneryService } from '../../scenery.service';

describe('NewScenePage', () => {
  let component: NewScenePage;
  let fixture: ComponentFixture<NewScenePage>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ NewScenePage ],
      imports: [ReactiveFormsModule, IonicModule.forRoot()],
      providers: [
        { provide: SceneryService, useValue: { createScene: jasmine.createSpy('createScene').and.returnValue(of({})) } },
        { provide: NavController, useValue: { navigateBack: jasmine.createSpy('navigateBack') } }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(NewScenePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should not submit an invalid scene form', () => {
    component.onSave();

    expect(TestBed.inject(SceneryService).createScene).not.toHaveBeenCalled();
  });

  it('should create a scene and navigate back after a valid submission', () => {
    const service = TestBed.inject(SceneryService);
    const navController = TestBed.inject(NavController);
    component.sceneForm.setValue({
      title: 'New scene',
      description: 'A description',
      imageUrl: 'https://example.com/image.jpg',
      rating: 8
    });

    component.onSave();

    expect(service.createScene).toHaveBeenCalledWith(component.sceneForm.getRawValue());
    expect(navController.navigateBack).toHaveBeenCalledWith('/scenery/tabs/my-scenes');
  });
});
