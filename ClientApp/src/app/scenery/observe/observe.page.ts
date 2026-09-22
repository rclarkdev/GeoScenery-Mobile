import { Component, OnInit } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { Geolocation } from '@capacitor/geolocation';
import { SceneryService } from '../scenery.service';
import { Scene } from '../scene.model';

@Component({
  selector: 'app-observe',
  templateUrl: './observe.page.html',
  styleUrls: ['./observe.page.scss'],
})
export class ObservePage implements OnInit {

  loadedScenery: Scene[] = [];
  isLocating = false;
  locationError = false;
  isFiltered = false;

  readonly searchForm = this.formBuilder.nonNullable.group({
    tags: [''],
    latitude: this.formBuilder.control<number | null>(null),
    longitude: this.formBuilder.control<number | null>(null),
    radiusKm: this.formBuilder.control<number | null>(null)
  });

  constructor(
    private formBuilder: FormBuilder,
    private sceneryService: SceneryService
  ) { }

  ngOnInit() {
    this.sceneryService.getScenery().subscribe(scenery => this.loadedScenery = scenery);
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
    }).subscribe(scenery => this.loadedScenery = scenery);
  }

  clearSearch() {
    this.searchForm.reset({ tags: '', latitude: null, longitude: null, radiusKm: null });
    this.isFiltered = false;
    this.sceneryService.getScenery().subscribe(scenery => this.loadedScenery = scenery);
  }
}
