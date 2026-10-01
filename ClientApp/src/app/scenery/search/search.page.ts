import { AfterViewInit, Component, ElementRef, Inject, InjectionToken, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Geolocation } from '@capacitor/geolocation';
import * as L from 'leaflet';
import { UserService } from '../../auth/user.service';
import { SceneryService } from '../scenery.service';
import { Scene } from '../scene.model';
import { createScenePopupContent } from './scene-popup';

export const GEOLOCATION = new InjectionToken<Pick<typeof Geolocation, 'getCurrentPosition'>>(
  'GEOLOCATION',
  { providedIn: 'root', factory: () => Geolocation }
);

@Component({
  selector: 'app-search',
  templateUrl: './search.page.html',
  styleUrls: ['./search.page.scss'],
})
export class SearchPage implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('mapContainer') mapContainerRef?: ElementRef<HTMLDivElement>;

  loadedScenery: Scene[] = [];
  isLocating = false;
  locationError = false;
  isFiltered = false;
  isFilterPanelOpen = false;
  isSearching = false;
  searchError = false;
  activeFilterCount = 0;

  private map?: any;
  private sceneMarkers: any[] = [];
  private searchMarker?: any;
  private searchCircle?: any;
  private popupCloseTimer?: ReturnType<typeof setTimeout>;
  private pinnedPopupMarker?: any;
  private mapInitializationTimer?: ReturnType<typeof setTimeout>;
  private initialMapCenter: L.LatLngExpression = [0, 0];
  private initialMapZoom = 2;
  private hasEnteredView = false;

  readonly searchForm = this.formBuilder.nonNullable.group({
    tags: [''],
    latitude: this.formBuilder.control<number | null>(null, [Validators.min(-90), Validators.max(90)]),
    longitude: this.formBuilder.control<number | null>(null, [Validators.min(-180), Validators.max(180)]),
    radiusKm: this.formBuilder.control<number | null>(null, [Validators.min(0.1)])
  });

  get hasInvalidCoordinates(): boolean {
    const { latitude, longitude } = this.searchForm.getRawValue();
    return this.searchForm.controls.latitude.invalid
      || this.searchForm.controls.longitude.invalid
      || (latitude == null) !== (longitude == null);
  }

  get radiusNeedsLocation(): boolean {
    const { latitude, longitude, radiusKm } = this.searchForm.getRawValue();
    return radiusKm != null && (latitude == null || longitude == null);
  }

  get canSearch(): boolean {
    return this.searchForm.valid && !this.hasInvalidCoordinates && !this.radiusNeedsLocation;
  }

  get hasFilterValues(): boolean {
    const { tags, latitude, longitude, radiusKm } = this.searchForm.getRawValue();
    return tags.trim().length > 0 || latitude != null || longitude != null || radiusKm != null;
  }

  constructor(
    private formBuilder: FormBuilder,
    private router: Router,
    private sceneryService: SceneryService,
    private userService: UserService,
    @Inject(GEOLOCATION) private geolocation: Pick<typeof Geolocation, 'getCurrentPosition'>
  ) { }

  ngOnInit() {
    this.refreshViewData();
  }

  ionViewWillEnter(): void {
    if (!this.hasEnteredView) {
      this.hasEnteredView = true;
      return;
    }

    this.refreshViewData();
  }

  private refreshViewData(): void {
    const { tags, latitude, longitude, radiusKm } = this.searchForm.getRawValue();
    const tagList = tags.split(',').map(tag => tag.trim()).filter(tag => tag.length > 0);
    const sceneryRequest = this.isFiltered
      ? this.sceneryService.searchScenery({
          tags: tagList,
          latitude: latitude ?? undefined,
          longitude: longitude ?? undefined,
          radiusKm: radiusKm ?? undefined
        })
      : this.sceneryService.getScenery();

    sceneryRequest.subscribe({
      next: scenery => {
        this.loadedScenery = scenery;
        this.searchError = false;
        this.updateMap();
      },
      error: () => this.searchError = true
    });

    this.userService.getCurrentUser().subscribe({
      next: user => {
        if (user.latitude == null || user.longitude == null) {
          return;
        }

        this.initialMapCenter = [user.latitude, user.longitude];
        this.initialMapZoom = 12;
        if (this.loadedScenery.some(scene => scene.latitude != null && scene.longitude != null)) {
          this.updateMap();
        } else {
          this.map?.setView(this.initialMapCenter, this.initialMapZoom);
        }
      },
      error: () => undefined
    });
  }

  ngAfterViewInit() {
    this.scheduleMapInitialization();
  }

  ionViewDidEnter(): void {
    this.scheduleMapInitialization();
  }

  ngOnDestroy(): void {
    this.cancelPopupClose();
    clearTimeout(this.mapInitializationTimer);
    this.map?.remove();
  }

  toggleFilters(): void {
    this.isFilterPanelOpen = !this.isFilterPanelOpen;
  }

  async useCurrentLocation() {
    this.isLocating = true;
    this.locationError = false;
    try {
      const position = await this.geolocation.getCurrentPosition();
      this.searchForm.patchValue({
        latitude: position.coords.latitude,
        longitude: position.coords.longitude
      });
      this.searchForm.controls.latitude.markAsTouched();
      this.searchForm.controls.longitude.markAsTouched();
    } catch {
      this.locationError = true;
    } finally {
      this.isLocating = false;
    }
  }

  onSearch(): void {
    if (!this.canSearch || this.isSearching) {
      this.searchForm.markAllAsTouched();
      return;
    }

    const { tags, latitude, longitude, radiusKm } = this.searchForm.getRawValue();
    const tagList = tags.split(',').map(tag => tag.trim()).filter(tag => tag.length > 0);
    const filterCount = Number(tagList.length > 0) + Number(latitude != null && longitude != null) + Number(radiusKm != null);

    this.isSearching = true;
    this.searchError = false;
    this.sceneryService.searchScenery({
      tags: tagList,
      latitude: latitude ?? undefined,
      longitude: longitude ?? undefined,
      radiusKm: radiusKm ?? undefined
    }).subscribe({
      next: scenery => {
        this.loadedScenery = scenery;
        this.isFiltered = true;
        this.activeFilterCount = filterCount;
        this.isSearching = false;
        this.isFilterPanelOpen = false;
        this.updateMap();
      },
      error: () => {
        this.isSearching = false;
        this.searchError = true;
      }
    });
  }

  clearSearch(): void {
    const shouldReload = this.isFiltered;
    this.searchForm.reset({ tags: '', latitude: null, longitude: null, radiusKm: null });
    this.isFiltered = false;
    this.activeFilterCount = 0;
    this.searchError = false;
    if (!shouldReload) {
      return;
    }

    this.isSearching = true;
    this.sceneryService.getScenery().subscribe({
      next: scenery => {
        this.loadedScenery = scenery;
        this.isSearching = false;
        this.updateMap();
      },
      error: () => {
        this.isSearching = false;
        this.searchError = true;
      }
    });
  }

  private initializeMap(): void {
    const container = this.mapContainerRef?.nativeElement;
    if (this.map || !container) {
      return;
    }

    L.Icon.Default.mergeOptions({
      iconRetinaUrl: 'assets/leaflet/marker-icon-2x.png',
      iconUrl: 'assets/leaflet/marker-icon.png',
      shadowUrl: 'assets/leaflet/marker-shadow.png'
    });
    this.map = L.map(container, { zoomControl: false }).setView(this.initialMapCenter, this.initialMapZoom);
    L.control.zoom({ position: 'bottomright' }).addTo(this.map);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors',
      maxZoom: 19
    }).addTo(this.map);
    this.updateMap();
  }

  private scheduleMapInitialization(attempt = 0): void {
    clearTimeout(this.mapInitializationTimer);
    this.mapInitializationTimer = setTimeout(() => {
      this.initializeMap();
      if (!this.map && attempt < 20) {
        this.scheduleMapInitialization(attempt + 1);
        return;
      }

      requestAnimationFrame(() => this.map?.invalidateSize());
    }, attempt === 0 ? 0 : 50);
  }

  private updateMap() {
    if (!this.map) {
      return;
    }

    this.cancelPopupClose();
    this.pinnedPopupMarker = undefined;
    this.sceneMarkers.forEach(marker => marker.remove());
    this.sceneMarkers = [];
    this.searchMarker?.remove();
    this.searchMarker = undefined;
    this.searchCircle?.remove();
    this.searchCircle = undefined;

    const bounds: any[] = [];

    for (const scene of this.loadedScenery) {
      if (scene.latitude == null || scene.longitude == null) {
        continue;
      }

      const position: [number, number] = [scene.latitude, scene.longitude];
      const marker = L.marker(position, {
        title: scene.title,
        alt: `${scene.title} map marker`
      })
        .bindPopup(() => createScenePopupContent(scene, () => {
          marker.closePopup();
          void this.router.navigate(['/scenery/tabs/search', scene.id]);
        }), {
          closeButton: false,
          className: 'scene-preview-popup'
        })
        .on('mouseover', () => {
          this.cancelPopupClose();
          marker.openPopup();
        })
        .on('mouseout', () => this.schedulePopupClose(marker))
        .on('click', () => {
          this.cancelPopupClose();
          this.pinnedPopupMarker = marker;
          marker.openPopup();
        })
        .on('popupopen', () => {
          const popupElement = marker.getPopup()?.getElement();
          if (!popupElement) {
            return;
          }

          popupElement.addEventListener('mouseenter', () => this.cancelPopupClose());
          popupElement.addEventListener('mouseleave', () => this.schedulePopupClose(marker));
          popupElement.addEventListener('focusin', () => this.cancelPopupClose());
          popupElement.addEventListener('focusout', () => this.schedulePopupClose(marker));
          popupElement.addEventListener('keydown', event => {
            if (event.key !== 'Escape') {
              return;
            }

            event.preventDefault();
            event.stopPropagation();
            this.cancelPopupClose();
            marker.getElement()?.focus();
            marker.closePopup();
          });
        })
        .on('popupclose', () => {
          if (this.pinnedPopupMarker === marker) {
            this.pinnedPopupMarker = undefined;
          }
        })
        .addTo(this.map);
      const markerElement = marker.getElement();
      markerElement?.addEventListener('focus', () => {
        this.cancelPopupClose();
        marker.openPopup();
      });
      markerElement?.addEventListener('blur', () => this.schedulePopupClose(marker));
      markerElement?.addEventListener('keydown', event => {
        if (event.key === 'Escape') {
          this.cancelPopupClose();
          marker.closePopup();
          return;
        }

        if (event.key !== 'Tab' && event.key !== 'Enter' && event.key !== ' ') {
          return;
        }

        event.preventDefault();
        this.cancelPopupClose();
        if (event.key === 'Enter' || event.key === ' ') {
          this.pinnedPopupMarker = marker;
        }
        marker.openPopup();
        setTimeout(() => {
          const popupElement = marker.getPopup()?.getElement() as HTMLElement | undefined;
          (popupElement?.querySelector('.scene-preview__action') as HTMLButtonElement | null)?.focus();
        }, 0);
      });
      this.sceneMarkers.push(marker);
      bounds.push(position);
    }

    const { latitude, longitude, radiusKm } = this.searchForm.getRawValue();
    if (this.isFiltered && latitude != null && longitude != null) {
      const searchPosition: [number, number] = [latitude, longitude];
      this.searchMarker = L.marker(searchPosition, { title: 'Search location' }).addTo(this.map);
      bounds.push(searchPosition);

      if (radiusKm != null) {
        this.searchCircle = L.circle(searchPosition, { radius: radiusKm * 1000, color: '#3880ff', fillOpacity: 0.1 }).addTo(this.map);
      }
    }

    if (bounds.length > 0) {
      this.map.fitBounds(L.latLngBounds(bounds).pad(0.2), { maxZoom: 14 });
    }
  }

  private schedulePopupClose(marker: any): void {
    if (this.pinnedPopupMarker === marker) {
      return;
    }

    this.cancelPopupClose();
    this.popupCloseTimer = setTimeout(() => marker.closePopup(), 180);
  }

  private cancelPopupClose(): void {
    if (this.popupCloseTimer) {
      clearTimeout(this.popupCloseTimer);
      this.popupCloseTimer = undefined;
    }
  }
}
