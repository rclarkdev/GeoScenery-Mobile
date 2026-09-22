import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular';
import { Geolocation } from '@capacitor/geolocation';
import { of } from 'rxjs';

import { ObservePage } from './observe.page';
import { SceneryService } from '../scenery.service';
import { Scene } from '../scene.model';

describe('ObservePage', () => {
  let component: ObservePage;
  let fixture: ComponentFixture<ObservePage>;
  let sceneryService: jasmine.SpyObj<SceneryService>;

  beforeEach(waitForAsync(() => {
    sceneryService = jasmine.createSpyObj('SceneryService', ['getScenery', 'searchScenery']);
    sceneryService.getScenery.and.returnValue(of([new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8)]));
    sceneryService.searchScenery.and.returnValue(of([]));

    TestBed.configureTestingModule({
      declarations: [ ObservePage ],
      imports: [ReactiveFormsModule, IonicModule.forRoot()],
      providers: [{ provide: SceneryService, useValue: sceneryService }],
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

  it('searches scenery using the form values and marks results as filtered', () => {
    component.searchForm.setValue({ tags: 'sunset, beach', latitude: 1, longitude: 2, radiusKm: 5 });

    component.onSearch();

    expect(sceneryService.searchScenery).toHaveBeenCalledWith({
      tags: ['sunset', 'beach'],
      latitude: 1,
      longitude: 2,
      radiusKm: 5
    });
    expect(component.isFiltered).toBe(true);
  });

  it('clears the search and reloads all scenery', () => {
    component.searchForm.setValue({ tags: 'sunset', latitude: 1, longitude: 2, radiusKm: 5 });
    component.isFiltered = true;

    component.clearSearch();

    expect(component.isFiltered).toBe(false);
    expect(component.searchForm.value.tags).toBe('');
    expect(sceneryService.getScenery).toHaveBeenCalledTimes(2);
  });

  it('sets locationError when useCurrentLocation fails', async () => {
    spyOn(Geolocation, 'getCurrentPosition').and.rejectWith(new Error('denied'));

    await component.useCurrentLocation();

    expect(component.locationError).toBe(true);
  });
});
