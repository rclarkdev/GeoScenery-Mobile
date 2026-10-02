import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { SupportContactService } from '../shared/support-contact.service';

interface LegalSection {
  heading: string;
  body: string;
  linkText?: string;
  linkUrl?: string;
}

interface CommunityGuideline {
  icon: string;
  heading: string;
  body: string;
}

interface SupportTopic {
  icon: string;
  heading: string;
  body: string;
}

@Component({
  selector: 'app-legal',
  templateUrl: './legal.page.html',
  styleUrls: ['./legal.page.scss']
})
export class LegalPage implements OnInit {
  title = '';
  documentKey = 'privacy';
  sections: LegalSection[] = [];
  readonly policyLastUpdated = 'October 2, 2026';
  supportForm = { name: '', email: '', topic: '', message: '', website: '' };
  isSubmittingSupport = false;
  supportError: string | null = null;
  supportSuccess: string | null = null;

  readonly guidelines: CommunityGuideline[] = [
    {
      icon: 'people-outline',
      heading: 'Treat people respectfully',
      body: 'Be considerate in profiles, messages, ratings, and feedback. Harassment, threats, hate, or attempts to evade someone’s block are not welcome.'
    },
    {
      icon: 'leaf-outline',
      heading: 'Share responsibly',
      body: 'Only post photos and details you have the right to share. Respect people’s privacy and avoid exposing private homes, personal information, or sensitive locations without permission.'
    },
    {
      icon: 'star-outline',
      heading: 'Keep ratings honest',
      body: 'Rate scenes in good faith. Do not use multiple accounts, spam, or coordinate activity to mislead people or manipulate ratings.'
    },
    {
      icon: 'hand-left-outline',
      heading: 'Use messages appropriately',
      body: 'Do not use messages to harass, spam, threaten, impersonate, or send unwanted commercial solicitations. Do not scrape or interfere with the service.'
    }
  ];

  readonly supportTopics: SupportTopic[] = [
    {
      icon: 'person-circle-outline',
      heading: 'Account, sign-in, or verification',
      body: 'Describe what you were trying to do and what happened. Never include your password, verification code, or reset token.'
    },
    {
      icon: 'shield-checkmark-outline',
      heading: 'Privacy or safety',
      body: 'For a scene or profile concern, use the in-app Report scene or Report profile action where available.'
    },
    {
      icon: 'construct-outline',
      heading: 'App or technical problem',
      body: 'Tell us what happened, the steps that led to it, and your device and app details.'
    },
    {
      icon: 'trash-outline',
      heading: 'Delete your account',
      body: 'Open your profile, choose Delete account, and follow the confirmation steps.'
    }
  ];

  private readonly documents: Record<string, { title: string; sections: LegalSection[] }> = {
    privacy: {
      title: 'Privacy Policy',
      sections: [
        { heading: 'Information in your account', body: 'We store your display name and email address, plus profile details you choose to add, such as a photo, bio, birth date, education, employment, hobbies, and optional coordinates.' },
        { heading: 'Scenes and activity', body: 'We store scene photos, titles, descriptions, tags, visibility choices, and optional coordinates. We also store ratings and feedback, follows and blocks, messages, and visits you choose to record (including the scene and visit time). Search requests may include location, distance, or tag filters you select.' },
        { heading: 'How information is used', body: 'We use account and scene information to run the service: sign-in and verification, showing profiles and public scenes, map and tag search, messaging, visits, ratings, moderation, support, and service operations.' },
        { heading: 'What others can see', body: 'Public profiles can show the profile details you provide, including your display name, photo, bio, birth date, education, employment, hobbies, and profile coordinates. Public scenes can show their photo, title, description, tags, and coordinates. Ratings and written feedback on public scenes are shown with your display name. Your email address is not shown on your public profile. Private scenes are not publicly searchable.' },
        { heading: 'Location and map services', body: 'Adding coordinates to a scene or profile is optional. If you use location-based search or choose to add your current location, the app requests device location permission. Map backgrounds are provided by OpenStreetMap; when the map loads, your device requests map tiles from its servers, which receive the technical information needed to serve those requests. Review OpenStreetMap’s privacy information for its practices.', linkText: 'OpenStreetMap Privacy Policy', linkUrl: 'https://wiki.osmfoundation.org/wiki/Privacy_Policy' },
        { heading: 'Messages, reports, and support', body: 'Messages are stored so conversations can be delivered and displayed to their participants. Reports are stored for moderation and include the report description and reporter contact details. Support form messages are emailed to active, verified GeoScenery administrators; the app does not create a support ticket record. The email delivery provider configured by the service operator processes email content and addresses to deliver these messages.' },
          { heading: 'Messages, reports, and support', body: 'Messages are stored so conversations can be delivered and displayed to their participants. Reports are stored for moderation and include the report description and reporter contact details. Support form messages are emailed to active, verified GeoScenery administrators; the app does not create a support ticket record. The configured email delivery provider also processes addresses and message content for verification, password resets, reports, and support.' },
          { heading: 'Administrators and service logs', body: 'Authorized administrators can review content reports, manage accounts and scenes, and review administrative audit records. Audit entries can include an administrator’s name and email, action details, reasons, and before/after snapshots. The service also records operational request details such as account ID when available, request path, method, response status, timing, and error or audit metadata. Fixed retention periods for these records and messages are not currently stated in the app; email copies may remain in administrator mailboxes according to the configured mail provider.' },
        { heading: 'Permissions', body: 'The app may request camera or photo-library access when you add images, and location access when you use location features. You can deny or revoke these permissions in your device settings; features that rely on them may then be unavailable.' },
        { heading: 'Account deletion', body: 'You can delete your account from your profile. Deletion removes your account and user-linked records such as visits, messages, follows, blocks, and ratings. Scenes you created may remain available without a link to your profile. Moderation reports and administrative audit records may be retained, and some report records preserve the name and email submitted with the report. Retention periods for those records are not currently specified in the app.' },
        { heading: 'Contact', body: 'Use the Support form in GeoScenery to contact active administrators. Do not include your password, verification code, or password-reset token.' }
      ]
    },
    terms: {
      title: 'Terms of Service',
      sections: [
        { heading: 'Accounts and access', body: 'Provide accurate account details, verify your email address, keep your sign-in credentials private, and use your account lawfully. You are responsible for activity carried out through your account. Do not access or attempt to access another person’s account.' },
        { heading: 'Your content and permission to use it', body: 'You retain ownership of content you submit. You give GeoScenery permission to store, process, and display that content as needed to operate the service and the features you choose to use. Only upload photos, text, and location details that you have the right to share.' },
        { heading: 'Public and private sharing', body: 'You choose whether each scene is public or private. Public scenes and the profile information you provide may be visible to other users. Location is optional, but coordinates you add may be shown with the scene or profile. Private scenes are not publicly searchable.' },
        { heading: 'Ratings, visits, and messages', body: 'Ratings and feedback should reflect your genuine experience. Visits are added only when you choose to mark a place as visited, and you can remove them later. Messages must not be used for harassment, threats, spam, impersonation, or unwanted solicitation.' },
        { heading: 'Prohibited use', body: 'Do not post illegal, threatening, hateful, harassing, sexually explicit, infringing, deceptive, or malicious content. Do not expose another person’s private information or sensitive location without permission, manipulate ratings, create accounts to evade restrictions, scrape the service, or interfere with its operation.' },
          { heading: 'Prohibited use', body: 'Do not post illegal, threatening, hateful, harassing, sexually explicit, infringing, deceptive, or malicious content. Do not expose another person’s private information or sensitive location without permission, manipulate ratings, create accounts to evade restrictions, scrape the service, or interfere with its operation.' },
        { heading: 'Reports and enforcement', body: 'Use the in-app Report scene or Report profile controls for concerns. Authorized administrators may review reports, hide or remove content, or suspend accounts when needed to enforce these terms or protect the community. Reports and appeals do not have a guaranteed review time or outcome; use the Support form if you believe an action was mistaken.' },
        { heading: 'Deleting an account', body: 'You can delete your account from your profile. Some user-linked records are removed, while scenes may remain without a profile link and moderation or administrative records may be retained as described in the Privacy Policy.' },
        { heading: 'Contact', body: 'Questions about these terms can be sent through the Support form in GeoScenery.' }
      ]
    }
  };

  constructor(private route: ActivatedRoute, private supportContactService: SupportContactService) { }

  submitSupportRequest(): void {
    if (this.isSubmittingSupport) {
      return;
    }

    this.isSubmittingSupport = true;
    this.supportError = null;
    this.supportSuccess = null;
    this.supportContactService.send({
      name: this.supportForm.name.trim(),
      email: this.supportForm.email.trim(),
      topic: this.supportForm.topic,
      message: this.supportForm.message.trim(),
      website: this.supportForm.website
    }).subscribe({
      next: response => {
        this.isSubmittingSupport = false;
        this.supportSuccess = response.message;
        this.supportForm = { name: '', email: '', topic: '', message: '', website: '' };
      },
      error: (error: HttpErrorResponse) => {
        this.isSubmittingSupport = false;
        if (error.status === 429) {
          this.supportError = 'Too many messages were sent from this connection. Please try again later.';
        } else if (error.status === 503) {
          this.supportError = 'Support email is temporarily unavailable. Please try again later.';
        } else if (error.status === 400) {
          this.supportError = 'Check the form details and try again.';
        } else {
          this.supportError = 'We could not send your message. Please try again.';
        }
      }
    });
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const key = params.get('document') ?? 'privacy';
      this.documentKey = key === 'support' || key === 'community' ? key : (this.documents[key] ? key : 'privacy');
      const document = this.documents[this.documentKey] ?? {
        title: this.documentKey === 'support' ? 'Support' : 'Community Guidelines',
        sections: []
      };
      this.title = document.title;
      this.sections = document.sections;
    });
  }
}