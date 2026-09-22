import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { IonicModule, NavController } from '@ionic/angular';
import { of } from 'rxjs';

import { EditScenePage } from './edit-scene.page';
import { SceneryService } from '../../scenery.service';
import { Scene } from '../../scene.model';

describe('EditScenePage', () => {
  let component: EditScenePage;
  let fixture: ComponentFixture<EditScenePage>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [ EditScenePage ],
      imports: [ReactiveFormsModule, IonicModule.forRoot()],
      providers: [
        { provide: ActivatedRoute, useValue: { paramMap: of(new Map([['sceneId', '1']])) } },
        { provide: SceneryService, useValue: {
          getScene: jasmine.createSpy('getScene').and.returnValue(of(new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8))),
          updateScene: jasmine.createSpy('updateScene').and.returnValue(of(new Scene(1, 'Updated scene', 'Updated description', 'https://example.com/updated.jpg', 9)))
        } },
        { provide: NavController, useValue: { navigateBack: jasmine.createSpy('navigateBack') } }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(EditScenePage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.scene?.id).toBe(1);
  });

  it('should update the scene and navigate back after a valid submission', () => {
    const service = TestBed.inject(SceneryService);
    const navController = TestBed.inject(NavController);

    component.sceneForm.patchValue({ title: 'Updated scene', rating: 9 });
    component.onSave();

    expect(service.updateScene).toHaveBeenCalledWith(1, {
      title: 'Updated scene',
      description: 'Description',
      imageUrl: 'https://example.com/image.jpg',
      rating: 9,
      latitude: null,
      longitude: null,
      tags: []
    });
    expect(navController.navigateBack).toHaveBeenCalledWith('/scenery/tabs/my-scenes/1');
  });
});
