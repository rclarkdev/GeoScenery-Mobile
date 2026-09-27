import { AfterViewInit, Component, ElementRef, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { Geolocation } from '@capacitor/geolocation';
import { SceneryService } from '../scenery.service';
import { Scene } from '../scene.model';
import { createScenePopupContent } from './scene-popup';

@Component({
  selector: 'app-observe',
  templateUrl: './observe.page.html',
  styleUrls: ['./observe.page.scss'],
})
export class ObservePage implements OnInit, AfterViewInit, OnDestroy {
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
    private sceneryService: SceneryService
  ) { }

  ngOnInit() {
    this.sceneryService.getScenery().subscribe(scenery => {
      this.loadedScenery = scenery;
      this.updateMap();
    });
  }

  ngAfterViewInit() {
    // Leaflet is loaded globally via a CDN <script> tag (see index.html); it isn't present in the Karma test environment
    if (!this.mapContainerRef || typeof L === 'undefined') {
      return;
    }

    this.map = L.map(this.mapContainerRef.nativeElement, { zoomControl: false }).setView([0, 0], 2);
    L.control.zoom({ position: 'bottomright' }).addTo(this.map);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors',
      maxZoom: 19
    }).addTo(this.map);
    this.updateMap();
  }

  ionViewDidEnter(): void {
    setTimeout(() => this.map?.invalidateSize(), 0);
  }

  ngOnDestroy(): void {
    this.cancelPopupClose();
    this.map?.remove();
  }

  toggleFilters(): void {
    this.isFilterPanelOpen = !this.isFilterPanelOpen;
  }

  async useCurrentLocation() {
    this.isLocating = true;
    this.locationError = false;
    try {
      const position = await Geolocation.getCurrentPosition();
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
          void this.router.navigate(['/scenery/tabs/observe', scene.id]);
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
