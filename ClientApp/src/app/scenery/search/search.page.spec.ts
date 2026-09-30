import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular';
import { Geolocation } from '@capacitor/geolocation';
import { of } from 'rxjs';

import { User } from '../../auth/user.model';
import { UserService } from '../../auth/user.service';
import { SearchPage } from './search.page';
import { SceneryService } from '../scenery.service';
import { Scene } from '../scene.model';

describe('SearchPage', () => {
  let component: SearchPage;
  let fixture: ComponentFixture<SearchPage>;
  let sceneryService: jasmine.SpyObj<SceneryService>;
  let userService: jasmine.SpyObj<UserService>;

  beforeEach(waitForAsync(() => {
    sceneryService = jasmine.createSpyObj('SceneryService', ['getScenery', 'searchScenery']);
    sceneryService.getScenery.and.returnValue(of([new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8)]));
    sceneryService.searchScenery.and.returnValue(of([]));
    userService = jasmine.createSpyObj('UserService', ['getCurrentUser']);
    userService.getCurrentUser.and.returnValue(of(new User(1, 'Test user', 'test@example.com', undefined, 45, -93)));

    TestBed.configureTestingModule({
      declarations: [ SearchPage ],
      imports: [ReactiveFormsModule, IonicModule.forRoot()],
      providers: [
        { provide: SceneryService, useValue: sceneryService },
        { provide: UserService, useValue: userService }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA],
    })
    .compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(SearchPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.loadedScenery.length).toBe(1);
  });

  it('uses the logged-in user location as the initial map center', () => {
    expect(userService.getCurrentUser).toHaveBeenCalled();
    expect((component as any).initialMapCenter).toEqual([45, -93]);
    expect((component as any).initialMapZoom).toBe(12);
  });

  it('renders only the map and filter controls, without a scene feed', () => {
    expect(fixture.nativeElement.querySelector('.scene-map')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('ion-card')).toBeNull();
    expect(fixture.nativeElement.querySelector('ion-thumbnail')).toBeNull();
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
    expect(component.activeFilterCount).toBe(3);
    expect(component.isFilterPanelOpen).toBe(false);
  });

  it('does not count draft values as active filters', () => {
    component.searchForm.patchValue({ tags: 'hiking' });

    expect(component.activeFilterCount).toBe(0);
    expect(component.hasFilterValues).toBeTrue();
  });

  it('clears unapplied values without reloading scenery', () => {
    component.searchForm.patchValue({ tags: 'hiking' });

    component.clearSearch();

    expect(component.searchForm.controls.tags.value).toBe('');
    expect(sceneryService.getScenery).toHaveBeenCalledTimes(1);
  });

  it('does not search by distance without a location', () => {
    component.searchForm.patchValue({ radiusKm: 10 });

    component.onSearch();

    expect(component.radiusNeedsLocation).toBeTrue();
    expect(sceneryService.searchScenery).not.toHaveBeenCalled();
  });

  it('does not search with out-of-range coordinates', () => {
    component.searchForm.patchValue({ latitude: 91, longitude: 0 });

    component.onSearch();

    expect(component.hasInvalidCoordinates).toBeTrue();
    expect(sceneryService.searchScenery).not.toHaveBeenCalled();
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
