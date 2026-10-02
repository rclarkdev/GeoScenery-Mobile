import { CUSTOM_ELEMENTS_SCHEMA } from '@angular/core';
import { ComponentFixture, TestBed, waitForAsync } from '@angular/core/testing';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { RouterTestingModule } from '@angular/router/testing';
import { BehaviorSubject, of } from 'rxjs';

import { LegalPage } from './legal.page';
import { SupportContactService } from '../shared/support-contact.service';

describe('LegalPage', () => {
  let fixture: ComponentFixture<LegalPage>;
  let documentParams: BehaviorSubject<ReturnType<typeof convertToParamMap>>;
  let supportContactService: jasmine.SpyObj<SupportContactService>;

  beforeEach(waitForAsync(() => {
    documentParams = new BehaviorSubject(convertToParamMap({ document: 'support' }));
    supportContactService = jasmine.createSpyObj<SupportContactService>('SupportContactService', ['send']);
    supportContactService.send.and.returnValue(of({ message: 'Your message was sent to the GeoScenery administrators.' }));
    return TestBed.configureTestingModule({
      declarations: [LegalPage],
      imports: [CommonModule, FormsModule, IonicModule.forRoot(), RouterTestingModule],
      providers: [
        { provide: ActivatedRoute, useValue: { paramMap: documentParams.asObservable() } },
        { provide: SupportContactService, useValue: supportContactService }
      ],
      schemas: [CUSTOM_ELEMENTS_SCHEMA]
    }).compileComponents();
  }));

  beforeEach(() => {
    fixture = TestBed.createComponent(LegalPage);
    fixture.detectChanges();
  });

  function showDocument(document: string): void {
    documentParams.next(convertToParamMap({ document }));
    fixture.detectChanges();
  }

  it('renders the contact form and credential safety guidance', () => {
    const pageText = fixture.nativeElement.textContent;

    expect(pageText).toContain('Send to support');
    expect(pageText).toContain('Your message and reply email go to active GeoScenery administrators.');
    expect(pageText).toContain('Never include your password, verification code, or password-reset token.');
  });

  it('sends a support request and confirms successful delivery', () => {
    const page = fixture.componentInstance;
    page.supportForm = {
      name: ' Sam Explorer ',
      email: ' sam@example.com ',
      topic: 'Other',
      message: '  I need help with my account.  ',
      website: ''
    };

    page.submitSupportRequest();

    expect(supportContactService.send).toHaveBeenCalledWith({
      name: 'Sam Explorer',
      email: 'sam@example.com',
      topic: 'Other',
      message: 'I need help with my account.',
      website: ''
    });
    expect(page.supportSuccess).toContain('sent');
    expect(page.isSubmittingSupport).toBeFalse();
  });

  it('explains respectful sharing and in-app reporting without promising an outcome', () => {
    showDocument('community');
    const pageText = fixture.nativeElement.textContent;

    expect(pageText).toContain('Treat people respectfully');
    expect(pageText).toContain('Harassment, threats, hate');
    expect(pageText).toContain('Keep ratings honest');
    expect(pageText).toContain('Report scene or Report profile');
    expect(pageText).toContain('Reports do not guarantee a particular review timeline or outcome.');
    expect(pageText).toContain('Privacy Policy');
    expect(pageText).toContain('Terms of Service');
  });

  it('explains collected activity, visibility, external maps, and deletion', () => {
    showDocument('privacy');
    const pageText = fixture.nativeElement.textContent;

    expect(pageText).toContain('Information in your account');
    expect(pageText).toContain('visits you choose to record');
    expect(pageText).toContain('shown with your display name');
    expect(pageText).toContain('OpenStreetMap');
    expect(pageText).toContain('Fixed retention periods');
    expect(fixture.nativeElement.querySelector('a[href="https://wiki.osmfoundation.org/wiki/Privacy_Policy"]')?.getAttribute('rel'))
      .toContain('noopener');
  });

  it('updates terms to address public sharing, messaging, visits, and moderation', () => {
    showDocument('terms');
    const pageText = fixture.nativeElement.textContent;

    expect(pageText).toContain('Public and private sharing');
    expect(pageText).toContain('Ratings, visits, and messages');
    expect(pageText).toContain('Reports and enforcement');
    expect(pageText).toContain('Deleting an account');
  });
});
