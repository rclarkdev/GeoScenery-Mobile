import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { IonicModule } from '@ionic/angular';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';

import { User } from '../../auth/user.model';
import { UserService } from '../../auth/user.service';
import { GEOLOCATION, SearchPage } from './search.page';
import { SceneryService } from '../scenery.service';
import { Scene } from '../scene.model';
import { ImageUrlPipe } from '../../shared/image-url.pipe';

describe('SearchPage', () => {
  let component: SearchPage;
  let fixture: ComponentFixture<SearchPage>;
  let sceneryService: jasmine.SpyObj<SceneryService>;
  let userService: jasmine.SpyObj<UserService>;
  let geolocation: jasmine.SpyObj<Pick<typeof import('@capacitor/geolocation').Geolocation, 'getCurrentPosition'>>;
  let queryParams: Map<string, string>;

  beforeEach(waitForAsync(() => {
    queryParams = new Map<string, string>();
    sceneryService = jasmine.createSpyObj('SceneryService', ['getScenery', 'searchScenery']);
    sceneryService.getScenery.and.returnValue(of([new Scene(1, 'Test scene', 'Description', 'https://example.com/image.jpg', 8)]));
    sceneryService.searchScenery.and.returnValue(of([]));
    userService = jasmine.createSpyObj('UserService', ['getCurrentUser']);
    userService.getCurrentUser.and.returnValue(of(new User(1, 'Test user', 'test@example.com', undefined, 45, -93)));
    geolocation = jasmine.createSpyObj('Geolocation', ['getCurrentPosition']);

    TestBed.configureTestingModule({
      declarations: [ SearchPage ],
      imports: [ReactiveFormsModule, IonicModule.forRoot(), RouterTestingModule, ImageUrlPipe],
      providers: [
        { provide: SceneryService, useValue: sceneryService },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: queryParams } } },
        { provide: UserService, useValue: userService },
        { provide: GEOLOCATION, useValue: geolocation }
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

  it('refreshes all scenes when returning to the cached Search view', () => {
    const newScene = new Scene(2, 'New scene', 'Created by another user', 'https://example.com/new.jpg', 9, 45, -93);
    sceneryService.getScenery.and.returnValue(of([newScene]));

    component.ionViewWillEnter();
    component.ionViewWillEnter();

    expect(sceneryService.getScenery).toHaveBeenCalledTimes(2);
    expect(component.loadedScenery).toEqual([newScene]);
  });

  it('reruns active filters when returning to the cached Search view', () => {
    component.searchForm.setValue({ tags: 'sunset', latitude: 45, longitude: -93, radiusKm: 10 });
    component.isFiltered = true;

    component.ionViewWillEnter();
    component.ionViewWillEnter();

    expect(sceneryService.searchScenery).toHaveBeenCalledWith({
      tags: ['sunset'],
      latitude: 45,
      longitude: -93,
      radiusKm: 10
    });
  });

  it('keeps returned scene locations in view when the user profile loads later', () => {
    clearTimeout((component as any).mapInitializationTimer);
    sceneryService.getScenery.and.returnValue(of([
      new Scene(2, 'Shared scene', 'Created by another user', 'https://example.com/shared.jpg', 9, 44, -92)
    ]));
    const updateMap = spyOn<any>(component, 'updateMap');
    (component as any).map = {
      setView: jasmine.createSpy('setView'),
      remove: jasmine.createSpy('remove')
    };

    (component as any).refreshViewData();

    expect(updateMap).toHaveBeenCalled();
    expect((component as any).map.setView).not.toHaveBeenCalled();
  });

  it('renders image-led results by default while retaining map mode and filters', () => {
    expect(fixture.nativeElement.querySelector('.scene-map')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.scene-results')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.scene-card')?.textContent).toContain('Test scene');
    expect(fixture.nativeElement.querySelector('.view-switch')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.filters-button')).not.toBeNull();
  });

  it('shows scenes without coordinates in browse mode', () => {
    const sceneWithoutLocation = new Scene(2, 'Unlocated scene', 'A description', '', 8);
    component.loadedScenery = [sceneWithoutLocation];
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.scene-card')?.textContent).toContain('Unlocated scene');
    expect(fixture.nativeElement.querySelector('.scene-card__media--empty')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.scene-card img')).toBeNull();
  });

  it('opens map mode and schedules map sizing', () => {
    const scheduleMapInitialization = spyOn<any>(component, 'scheduleMapInitialization');

    component.setViewMode('map');

    expect(component.viewMode).toBe('map');
    expect(scheduleMapInitialization).toHaveBeenCalled();
  });

  it('applies a nearby-search link when returning to the cached Search route', () => {
    queryParams.set('latitude', '45.5');
    queryParams.set('longitude', '-93.25');
    queryParams.set('radiusKm', '10');

    component.ionViewWillEnter();
    component.ionViewWillEnter();

    expect(component.searchForm.getRawValue()).toEqual({
      tags: '', latitude: 45.5, longitude: -93.25, radiusKm: 10
    });
    expect(sceneryService.searchScenery).toHaveBeenCalledWith({
      tags: [], latitude: 45.5, longitude: -93.25, radiusKm: 10
    });
  });

  it('shows an empty state when a search returns no scenes', () => {
    component.loadedScenery = [];
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.results-state')?.textContent).toContain('No scenery is available yet');
  });

  it('retries the active filtered query after an error', () => {
    component.isFiltered = true;
    component.searchForm.patchValue({ tags: 'coast', latitude: 47, longitude: -122, radiusKm: 10 });
    sceneryService.searchScenery.and.returnValue(of([]));

    component.retrySearch();

    expect(sceneryService.searchScenery).toHaveBeenCalledWith({
      tags: ['coast'], latitude: 47, longitude: -122, radiusKm: 10
    });
    expect(component.searchError).toBe(false);
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

  it('removes nearby-search parameters when clearing filters', () => {
    queryParams.set('latitude', '45.5');
    queryParams.set('longitude', '-93.25');
    queryParams.set('radiusKm', '10');
    const navigate = spyOn(component['router'], 'navigate').and.resolveTo(true);

    component.clearSearch();

    expect(navigate).toHaveBeenCalledWith([], {
      relativeTo: component['route'],
      queryParams: { latitude: null, longitude: null, radiusKm: null },
      queryParamsHandling: 'merge',
      replaceUrl: true
    });
  });

  it('sets locationError when useCurrentLocation fails', async () => {
    geolocation.getCurrentPosition.and.rejectWith(new Error('denied'));

    await component.useCurrentLocation();

    expect(geolocation.getCurrentPosition).toHaveBeenCalled();
    expect(component.locationError).toBe(true);
    expect(component.isLocating).toBe(false);
  });
});
