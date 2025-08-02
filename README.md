# EduVision API

![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)
![C# 12](https://img.shields.io/badge/C%23-12.0-blue.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

EduVision is a powerful AI-driven educational content generation platform that creates interactive slides and video lessons for Vietnamese educational content. The system leverages Google's Gemini AI, Azure cloud services, and modern web technologies to provide an automated solution for generating educational materials.

## 🌟 Features

### 📚 Content Generation
- **AI-Powered Slide Generation**: Create educational slides using Google Gemini AI
- **Video Lesson Creation**: Generate video lessons with synchronized audio narration
- **Multiple Templates**: Support for various presentation templates (Dark, Modern, Interactive)
- **Vietnamese Language Support**: Specialized for Vietnamese educational curriculum

### 🔐 User Management & Authentication
- **JWT-based Authentication**: Secure token-based authentication system
- **Role-based Access Control**: Support for USER, MANAGER, and ADMIN roles
- **Google OAuth Integration**: Social login capabilities
- **User Dashboard**: Comprehensive user statistics and content management

### 💰 Payment & Quota System
- **PayOS Integration**: Vietnamese payment gateway integration
- **Quota Management**: Monthly limits for slides and video generation
- **Usage Tracking**: Detailed quota usage statistics and history
- **Flexible Pricing**: Different quota limits for different user tiers

### 🎨 Media Processing
- **Azure Blob Storage**: Cloud-based file storage and management
- **Text-to-Speech**: Microsoft Cognitive Services for audio generation
- **Video Processing**: FFmpeg integration for video creation and manipulation
- **Image Management**: Automated image selection and processing

### 📊 Analytics & Monitoring
- **Admin Dashboard**: Comprehensive statistics and user management
- **Content Generation Analytics**: Track slide and video generation trends
- **User Activity Monitoring**: Monitor user engagement and usage patterns
- **Real-time Notifications**: Firebase Cloud Messaging integration

## 🏗️ Architecture

### Technology Stack
- **Backend**: ASP.NET Core 8.0 (C# 12)
- **Database**: SQL Server with Entity Framework Core
- **Content Database**: MongoDB for educational content storage
- **Message Queue**: Apache Kafka for asynchronous processing
- **AI Service**: Google Gemini API
- **Cloud Storage**: Azure Blob Storage
- **Authentication**: JWT with refresh token support
- **Payment**: PayOS gateway
- **Monitoring**: Azure Application Insights

### System Architecture