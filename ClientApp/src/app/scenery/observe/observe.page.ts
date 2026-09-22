import { AfterViewInit, Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Router } from '@angular/router';
import { Geolocation } from '@capacitor/geolocation';
import { SceneryService } from '../scenery.service';
import { Scene } from '../scene.model';

@Component({
  selector: 'app-observe',
  templateUrl: './observe.page.html',
  styleUrls: ['./observe.page.scss'],
})
export class ObservePage implements OnInit, AfterViewInit {
  @ViewChild('mapContainer') mapContainerRef?: ElementRef<HTMLDivElement>;

  loadedScenery: Scene[] = [];
  isLocating = false;
  locationError = false;
  isFiltered = false;

  private map?: any;
  private sceneMarkers: any[] = [];
  private searchMarker?: any;
  private searchCircle?: any;

  readonly searchForm = this.formBuilder.nonNullable.group({
    tags: [''],
    latitude: this.formBuilder.control<number | null>(null),
    longitude: this.formBuilder.control<number | null>(null),
    radiusKm: this.formBuilder.control<number | null>(null)
  });

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

    this.map = L.map(this.mapContainerRef.nativeElement).setView([0, 0], 2);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors',
      maxZoom: 19
    }).addTo(this.map);
    this.updateMap();
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
    } catch {
      this.locationError = true;
    } finally {
      this.isLocating = false;
    }
  }

  onSearch() {
    const { tags, latitude, longitude, radiusKm } = this.searchForm.getRawValue();
    const tagList = tags.split(',').map(tag => tag.trim()).filter(tag => tag.length > 0);

    this.isFiltered = true;
    this.sceneryService.searchScenery({
      tags: tagList,
      latitude: latitude ?? undefined,
      longitude: longitude ?? undefined,
      radiusKm: radiusKm ?? undefined
    }).subscribe(scenery => {
      this.loadedScenery = scenery;
      this.updateMap();
    });
  }

  clearSearch() {
    this.searchForm.reset({ tags: '', latitude: null, longitude: null, radiusKm: null });
    this.isFiltered = false;
    this.sceneryService.getScenery().subscribe(scenery => {
      this.loadedScenery = scenery;
      this.updateMap();
    });
  }

  private updateMap() {
    if (!this.map) {
      return;
    }

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
      const marker = L.marker(position)
        .bindPopup(`<strong>${scene.title}</strong>`)
        .on('click', () => this.router.navigate(['/scenery/tabs/observe', scene.id]))
        .addTo(this.map);
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
}
