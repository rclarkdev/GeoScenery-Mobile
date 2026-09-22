import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

interface LegalSection {
  heading: string;
  body: string;
}

@Component({
  selector: 'app-legal',
  templateUrl: './legal.page.html'
})
export class LegalPage implements OnInit {
  title = '';
  sections: LegalSection[] = [];

  private readonly documents: Record<string, { title: string; sections: LegalSection[] }> = {
    privacy: {
      title: 'Privacy Policy',
      sections: [
        { heading: 'Information we collect', body: 'GeoScenery collects account details such as your display name and email address, profile information you choose to provide, scene photos and descriptions, ratings, follows, and location data when you choose to use location features.' },
        { heading: 'How we use information', body: 'We use this information to authenticate you, display and search scenes, provide social features, improve reliability, and respond to support requests. We do not sell personal information.' },
        { heading: 'Photos and location', body: 'Photos and location are optional. A scene location and image may be visible to other users when you publish a scene. You can remove your account using the Delete account control in your profile.' },
        { heading: 'Retention and deletion', body: 'You may request deletion of your account and associated content from your profile. We retain only information required for legal, security, or operational purposes.' },
        { heading: 'Contact', body: 'For privacy questions or requests, contact support at support@geoscenery.example.' }
      ]
    },
    terms: {
      title: 'Terms of Service',
      sections: [
        { heading: 'Using GeoScenery', body: 'You must provide accurate account information, keep your credentials secure, and use the service lawfully. You are responsible for activity performed through your account.' },
        { heading: 'Your content', body: 'You retain ownership of content you submit. You grant GeoScenery permission to host, display, and process it to operate the service. Do not upload content you do not have permission to share.' },
        { heading: 'Prohibited content', body: 'Do not upload illegal, threatening, hateful, harassing, sexually explicit, infringing, deceptive, or malicious content. Do not misuse location data or attempt to access another account.' },
        { heading: 'Enforcement', body: 'We may remove content or suspend accounts that violate these terms, create safety risks, or abuse the service. Contact support if you believe an action was made in error.' },
        { heading: 'Contact', body: 'Questions about these terms can be sent to support@geoscenery.example.' }
      ]
    },
    community: {
      title: 'Community Guidelines',
      sections: [
        { heading: 'Be respectful', body: 'Treat other people and places with respect. Keep comments, profiles, ratings, and scene descriptions constructive.' },
        { heading: 'Share responsibly', body: 'Only share photos and locations that are safe and lawful to publish. Avoid exposing private homes, personal information, or sensitive locations without permission.' },
        { heading: 'No abuse or manipulation', body: 'Do not harass users, create fake accounts, manipulate ratings, spam, scrape the service, or interfere with the experience of others.' },
        { heading: 'Report concerns', body: 'Contact support@geoscenery.example with the account or scene involved and a description of the concern. We review reports and take appropriate action.' }
      ]
    },
    support: {
      title: 'Support',
      sections: [
        { heading: 'Get help', body: 'For account, safety, privacy, or technical questions, email support@geoscenery.example. Include your account email and a clear description of the issue. Do not send your password or reset token.' },
        { heading: 'Account deletion', body: 'To delete your account, open your profile, choose Delete account, and confirm. This removes your profile, ratings, follows, and account-owned data.' },
        { heading: 'Safety reports', body: 'For harmful or unlawful content, include the scene or profile link, what happened, and why it violates the Community Guidelines.' }
      ]
    }
  };

  constructor(private route: ActivatedRoute) { }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const document = this.documents[params.get('document') ?? 'privacy'] ?? this.documents.privacy;
      this.title = document.title;
      this.sections = document.sections;
    });
  }
}