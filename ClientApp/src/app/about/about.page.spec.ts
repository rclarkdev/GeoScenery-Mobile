import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { AboutPage } from './about.page';

describe('AboutPage', () => {
  let component: AboutPage;
  let fixture: ComponentFixture<AboutPage>;

  beforeEach(waitForAsync(() => {
    TestBed.configureTestingModule({
      declarations: [AboutPage],
      schemas: [CUSTOM_ELEMENTS_SCHEMA]
    }).compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(AboutPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('creates the about page with the confirmed product feature list', () => {
    expect(component).toBeTruthy();
    expect(component.features.length).toBe(4);
    expect(component.features.map(feature => feature.title)).toEqual([
      'Find a scene your way',
      'Leave a thoughtful rating',
      'Keep in touch',
      'Save places you like'
    ]);
  });

  it('includes the guide, privacy information, and practical FAQs', () => {
    const pageText = fixture.nativeElement.textContent;

    expect(pageText).toContain('A place, your way');
    expect(pageText).toContain('You choose what to share.');
    expect(pageText).toContain('Does GeoScenery track my visits?');
    expect(pageText).toContain('Visits are never added automatically');
    expect(pageText).toContain('Version 1.0.0');
  });
});