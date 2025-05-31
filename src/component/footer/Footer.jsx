const Footer = () => {
  const footerLinks = [
    {
      title: "Sản phẩm",
      links: [
        { label: "Tính năng", href: "#" },
        { label: "Mẫu", href: "#" },
        { label: "Thư viện", href: "#" },
        { label: "Công cụ", href: "#" },
        { label: "Ứng dụng di động", href: "#" },
      ],
    },
    {
      title: "Giải pháp",
      links: [
        { label: "Doanh nghiệp", href: "#" },
        { label: "Giáo dục", href: "#" },
        { label: "Tiếp thị", href: "#" },
        { label: "Truyền thông", href: "#" },
        { label: "Cá nhân", href: "#" },
      ],
    },
    {
      title: "Tài nguyên",
      links: [
        { label: "Blog", href: "#" },
        { label: "Hướng dẫn", href: "#" },
        { label: "Trung tâm trợ giúp", href: "#" },
        { label: "Cộng đồng", href: "#" },
        { label: "Đối tác", href: "#" },
      ],
    },
    {
      title: "Công ty",
      links: [
        { label: "Giới thiệu", href: "#" },
        { label: "Nghề nghiệp", href: "#" },
        { label: "Liên hệ", href: "#" },
        { label: "Tin tức", href: "#" },
        { label: "Nhà đầu tư", href: "#" },
      ],
    },
  ]

  const legalLinks = [
    { label: "Điều khoản dịch vụ", href: "#" },
    { label: "Chính sách bảo mật", href: "#" },
    { label: "Cookie", href: "#" },
    { label: "Sở hữu trí tuệ", href: "#" },
  ]

  return (
    <footer className="bg-white border-t border-gray-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="flex flex-col md:flex-row justify-between items-center">
          {/* Logo */}
          <div className="flex items-center space-x-2 mb-4 md:mb-0">
            <div className="w-8 h-8 bg-gradient-to-r from-purple-500 to-purple-600 rounded-lg flex items-center justify-center">
              <svg className="w-5 h-5 text-white" fill="currentColor" viewBox="0 0 24 24">
                <path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5" />
              </svg>
            </div>
            <span className="text-xl font-semibold text-gray-900">EduVision</span>
          </div>

          {/* Links */}
          <div className="flex flex-wrap justify-center gap-6 mb-4 md:mb-0">
            <a href="#" className="text-sm text-gray-600 hover:text-gray-900">
              Điều khoản
            </a>
            <a href="#" className="text-sm text-gray-600 hover:text-gray-900">
              Bảo mật
            </a>
            <a href="#" className="text-sm text-gray-600 hover:text-gray-900">
              Trợ giúp
            </a>
            <a href="#" className="text-sm text-gray-600 hover:text-gray-900">
              Liên hệ
            </a>
          </div>

          {/* Copyright */}
          <p className="text-sm text-gray-600">© 2025 EduVision</p>
        </div>
      </div>
    </footer>
  )
}

export default Footer
