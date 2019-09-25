import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { async, ComponentFixture, TestBed } from '@angular/core/testing';

import { EditScenePage } from './edit-scene.page';

describe('EditScenePage', () => {
  let component: EditScenePage;
  let fixture: ComponentFixture<EditScenePage>;

  beforeEach(async(() => {
    TestBed.configureTestingModule({
      declarations: [ EditScenePage ],
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
  });
});
